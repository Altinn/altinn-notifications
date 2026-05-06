using System.Net;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

/// <summary>
/// Integration tests for the TriggerController email notification endpoints.
/// Mirrors <see cref="Trigger_SendSmsNotificationsTests"/>: covers the schedule
/// gate on <c>/sendemaildaytime</c> and that <c>/sendemail</c> only processes
/// orders with policy = Anytime or NULL.
/// </summary>
public class Trigger_SendEmailNotificationsTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>, IAsyncLifetime
{
    private const string _sendEmailAnytimePath = "/notifications/api/v1/trigger/sendemail";
    private const string _sendEmailDaytimePath = "/notifications/api/v1/trigger/sendemaildaytime";

    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly DateTime _currentTime = DateTime.UtcNow.Date.AddHours(10);

    public Trigger_SendEmailNotificationsTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_topicName);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When the send email endpoint is triggered, we expect all email notifications with status 'New' to be pushed to the
    /// send email kafka topic and that each notification gets the result status 'Sending' in the database.
    /// </summary>
    [Fact]
    public async Task Post_Ok()
    {
        // Arrange — emailSendingTimePolicy = null mirrors today's clients that don't set the field.
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef);

        HttpClient client = GetTestClient();

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendEmailAnytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        var actual = await IntegrationTestUtil.PollSendingNotificationStatus(notification);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, actual);
    }

    /// <summary>
    /// /trigger/sendemail must NOT pick up orders explicitly tagged with Daytime — the
    /// existing cron path stays a no-op for those rows so the new daytime path can own them.
    /// </summary>
    [Fact]
    public async Task SendEmailAnytime_DoesNotProcessDaytimePolicyOrder()
    {
        // Arrange
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef, emailSendingTimePolicy: SendingTimePolicy.Daytime);

        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendEmailAnytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert — give the publish loop a beat to (potentially) pick the row up.
        await Task.Delay(500, TestContext.Current.CancellationToken);

        string sql = $"select count(1) from notifications.emailnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        long actual = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(0, actual);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Daytime endpoint outside the configured window must not process any notification,
    /// even for orders explicitly tagged with sendingTimePolicy = Daytime.
    /// </summary>
    [Fact]
    public async Task SendEmailDaytime_OutsideAllowedHours_DoesNotProcess()
    {
        // Arrange
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef, emailSendingTimePolicy: SendingTimePolicy.Daytime);

        HttpClient client = GetTestClient(canSendEmailNow: false);
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendEmailDaytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        string sql = $"select count(1) from notifications.emailnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        long actual = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(0, actual);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Daytime endpoint inside the configured window picks up Daytime orders.
    /// </summary>
    [Fact]
    public async Task SendEmailDaytime_InsideAllowedHours_ProcessesDaytimeOrder()
    {
        // Arrange
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef, emailSendingTimePolicy: SendingTimePolicy.Daytime);

        HttpClient client = GetTestClient(canSendEmailNow: true);
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendEmailDaytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        long actual = await IntegrationTestUtil.PollSendingNotificationStatus(notification);
        Assert.Equal(1L, actual);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient GetTestClient(bool canSendEmailNow = true)
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                // Set up temp topic
                services.Configure<KafkaSettings>(opts =>
                {
                    opts.Admin.TopicList = new List<string> { _topicName };
                });
                services.Configure<Altinn.Notifications.Core.Configuration.KafkaSettings>(opts =>
                {
                    opts.EmailQueueTopicName = _topicName;
                });

                // Configure test time
                Mock<IDateTimeService> dateMock = new();
                dateMock.Setup(e => e.UtcNow()).Returns(_currentTime);
                services.AddSingleton(dateMock.Object);

                // Mock schedule service when needed
                if (!canSendEmailNow)
                {
                    Mock<INotificationScheduleService> scheduleMock = new();
                    scheduleMock.Setup(e => e.CanSendEmailNow()).Returns(false);
                    services.AddSingleton(scheduleMock.Object);
                }

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
