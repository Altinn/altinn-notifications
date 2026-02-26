using System.Diagnostics;
using System.Text;

using Altinn.Notifications.Email.Configuration;
using Altinn.Notifications.Email.Telemetry;

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Altinn.Notifications.Email.Tests;

public class RequestBodyTelemetryMiddlewareTests
{
    private const string _realWorldDeliveryEvent = "[{\"id\":\"e000f000-0000-0000-0000-000000000000\",\"topic\":\"/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/microsoft.communication/communicationservices/{acs-resource-name}\",\"subject\":\"sender/sender@mydomain.com/message/f000e000-0000-0000-0000-000000000000\",\"eventType\":\"Microsoft.Communication.EmailDeliveryReportReceived\",\"data\":{\"sender\":\"sender@mydomain.com\",\"recipient\":\"recipient@example.com\",\"messageId\":\"f000e000-0000-0000-0000-000000000000\",\"status\":\"Delivered\",\"deliveryAttemptTimeStamp\":\"2025-11-11T13:58:00.0000000Z\",\"deliveryStatusDetails\":{\"statusMessage\":\"No error.\"}},\"dataVersion\":\"1.0\",\"metadataVersion\":\"1\",\"eventTime\":\"2025-11-11T13:58:00Z\"}]";
    private readonly string _validationEvent = "[{\"id\": \"2d1781af-3a4c-4d7c-bd0c-e34b19da4e66\",\"topic\": \"/subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx\",\"subject\": \"\",\"data\": {\"validationCode\": \"512d38b6-c7b8-40c8-89fe-f46f9e9622b6\",\"validationUrl\": \"https://rp-eastus2.eventgrid.azure.net:553/eventsubscriptions/myeventsub/validate?id=0000000000-0000-0000-0000-00000000000000&t=2022-10-28T04:23:35.1981776Z&apiVersion=2018-05-01-preview&token=1A1A1A1A\"},\"eventType\": \"Microsoft.EventGrid.SubscriptionValidationEvent\",\"eventTime\": \"2022-10-28T04:23:35.1981776Z\",\"metadataVersion\": \"1\",\"dataVersion\": \"1\"}]";
    private readonly Microsoft.Extensions.Options.IOptions<EmailDeliveryReportSettings> _options = Microsoft.Extensions.Options.Options.Create(new EmailDeliveryReportSettings());

    [Fact]
    public async Task InvokeAsync_ParseObjectSendOperationResults_AddsSendOperationResultsTag()
    {
        // Arrange
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        var (activitySource, activity) = CreateActivity();
        using (activitySource)
        using (activity)
        {
            var options = CreateOptions("sendoperationresults");
            var middleware = new RequestBodyTelemetryMiddleware(
                next: (innerHttpContext) => Task.CompletedTask,
                emailDeliveryReportSettings: options);
            var context = CreateHttpContext("POST", _realWorldDeliveryEvent);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(activity);
            var sendOperationResultsTag = activity.Tags.FirstOrDefault(t => t.Key == "SendOperationResults");
            Assert.NotEqual(default, sendOperationResultsTag);
            Assert.Contains("f000e000-0000-0000-0000-000000000000", sendOperationResultsTag.Value);
            Assert.Contains("\"SendResult\":2", sendOperationResultsTag.Value); // Delivered
        }
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        var (activitySource, activity) = CreateActivity();
        using (activitySource)
        using (activity)
        {
            bool nextMiddlewareCalled = false;
            var middleware = new RequestBodyTelemetryMiddleware(
                next: (innerHttpContext) =>
                {
                    nextMiddlewareCalled = true;
                    return Task.CompletedTask;
                },
                emailDeliveryReportSettings: _options);
            var context = CreateHttpContext("POST", _realWorldDeliveryEvent);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextMiddlewareCalled);
        }
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware_WhenActivityIsNull()
    {
        // Arrange
        // activity is implicitly null here since we are not creating one for listening
        bool nextMiddlewareCalled = false;
        var middleware = new RequestBodyTelemetryMiddleware(
            next: (innerHttpContext) =>
            {
                nextMiddlewareCalled = true;
                return Task.CompletedTask;
            },
            emailDeliveryReportSettings: _options);
        var context = CreateHttpContext("POST", _realWorldDeliveryEvent);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextMiddlewareCalled);
    }

    [Fact]
    public async Task InvokeAsync_PreservesRequestBodyForNextMiddleware()
    {
        // Arrange
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        var (activitySource, activity) = CreateActivity();
        using (activitySource)
        using (activity)
        {
            string? bodyReadByNextMiddleware = null;
            var middleware = new RequestBodyTelemetryMiddleware(
                next: async (innerHttpContext) =>
                {
                    using var reader = new StreamReader(innerHttpContext.Request.Body);
                    bodyReadByNextMiddleware = await reader.ReadToEndAsync();
                },
                emailDeliveryReportSettings: _options);
            var context = CreateHttpContext("POST", _realWorldDeliveryEvent);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(_realWorldDeliveryEvent, bodyReadByNextMiddleware);
        }
    }

    [Fact]
    public async Task InvokeAsync_ParseObjectSendOperationResults_NonDeliveryReportEvent_DoesNotAddTag()
    {
        // Arrange
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        var (activitySource, activity) = CreateActivity();
        using (activitySource)
        using (activity)
        {
            var options = CreateOptions("sendoperationresults");
            var middleware = new RequestBodyTelemetryMiddleware(
                next: (innerHttpContext) => Task.CompletedTask,
                emailDeliveryReportSettings: options);

            // Use a validation event which won't produce SendOperationResults
            var context = CreateHttpContext("POST", _validationEvent);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(activity);
            var sendOperationResultsTag = activity.Tags.FirstOrDefault(t => t.Key == "SendOperationResults");

            // No tag should be added when sendOperationResults list is empty
            Assert.Equal(default, sendOperationResultsTag);
        }
    }

    [Theory]
    [InlineData("invalid-email-address", "invalid-email-address", "Invalid email without @ symbol")]
    [InlineData("ab@example.com", "***@example.com", "Email with 2-character local part")]
    [InlineData("x@example.com", "***@example.com", "Email with 1-character local part")]
    public async Task InvokeAsync_ParseObjectDeliveryReport_SpecialEmailCases_HandlesCorrectly(
        string recipientEmail,
        string expectedMaskedEmail,
        string _)
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        var (activitySource, activity) = CreateActivity();
        using (activitySource)
        using (activity)
        {
            var options = CreateOptions("deliveryreport");
            var middleware = new RequestBodyTelemetryMiddleware(
                next: (innerHttpContext) => Task.CompletedTask,
                emailDeliveryReportSettings: options);

            // Event with the test recipient email address
            string deliveryEventWithSpecialEmail = $"[{{\"id\":\"e000f000-0000-0000-0000-000000000000\",\"topic\":\"/subscriptions/{{subscription-id}}/resourceGroups/{{resource-group}}/providers/microsoft.communication/communicationservices/{{acs-resource-name}}\",\"subject\":\"sender/sender@mydomain.com/message/f000e000-0000-0000-0000-000000000000\",\"eventType\":\"Microsoft.Communication.EmailDeliveryReportReceived\",\"data\":{{\"sender\":\"sender@mydomain.com\",\"recipient\":\"{recipientEmail}\",\"messageId\":\"f000e000-0000-0000-0000-000000000000\",\"status\":\"Delivered\",\"deliveryAttemptTimeStamp\":\"2025-11-11T13:58:00.0000000Z\",\"deliveryStatusDetails\":{{\"statusMessage\":\"No error.\"}}}},\"dataVersion\":\"1.0\",\"metadataVersion\":\"1\",\"eventTime\":\"2025-11-11T13:58:00Z\"}}]";
            var context = CreateHttpContext("POST", deliveryEventWithSpecialEmail);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(activity);
            var deliveryReportsTag = activity.Tags.FirstOrDefault(t => t.Key == "DeliveryReports");
            Assert.NotEqual(default, deliveryReportsTag);

            // Verify the email is handled according to expectations (masked or preserved)
            Assert.Contains(expectedMaskedEmail, deliveryReportsTag.Value);
            Assert.Contains("f000e000-0000-0000-0000-000000000000", deliveryReportsTag.Value);
        }
    }

    private static ActivityListener CreateListener()
    {
        // Arrange
        return new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
    }

    private static DefaultHttpContext CreateHttpContext(string method, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/json";
        return context;
    }

    private static (ActivitySource ActivitySource, Activity Activity) CreateActivity()
    {
        var activitySource = new ActivitySource("TestSource");
        var activity = activitySource.StartActivity("TestActivity");
        Activity.Current = activity;
        return (activitySource, activity!);
    }

    private static Microsoft.Extensions.Options.IOptions<EmailDeliveryReportSettings> CreateOptions(string parseObject)
    {
        return Microsoft.Extensions.Options.Options.Create(new EmailDeliveryReportSettings
        {
            ParseObject = parseObject
        });
    }
}
