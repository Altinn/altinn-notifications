using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.AcsMock;

using Azure;
using Azure.Communication.Email;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

public class AcsMockTests(AcsMockFactory factory) : IClassFixture<AcsMockFactory>
{
    private const string Sender = "noreply@altinn.example";

    [Fact]
    public async Task Send_WithRealEmailClient_Succeeds()
    {
        var client = factory.CreateEmailClient();

        var operation = await client.SendAsync(WaitUntil.Started, Message("ola.nordmann@example.com"), TestContext.Current.CancellationToken);
        await operation.UpdateStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(Guid.TryParse(operation.Id, out _));
        Assert.True(operation.HasCompleted);
        Assert.Equal(EmailSendStatus.Succeeded, operation.Value.Status);
    }

    [Fact]
    public async Task Send_WithWrongAccessKey_ReturnsUnauthorized()
    {
        var client = factory.CreateEmailClient(Convert.ToBase64String(Encoding.ASCII.GetBytes("wrong-key")));

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => client.SendAsync(WaitUntil.Started, Message("ola.nordmann@example.com"), TestContext.Current.CancellationToken));

        Assert.Equal(401, e.Status);
        Assert.Equal("Denied", e.ErrorCode);
    }

    [Fact]
    public async Task GetOperation_WithWrongAccessKey_ReturnsUnauthorized()
    {
        var operation = await factory.CreateEmailClient().SendAsync(WaitUntil.Started, Message("ola.nordmann@example.com"), TestContext.Current.CancellationToken);
        var wrongClient = factory.CreateEmailClient(Convert.ToBase64String(Encoding.ASCII.GetBytes("wrong-key")));

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => new EmailSendOperation(operation.Id, wrongClient).UpdateStatusAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(401, e.Status);
    }

    [Fact]
    public async Task Send_WithTamperedBody_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        string body = """{"senderAddress":"a@b.no","content":{"subject":"s"},"recipients":{"to":[{"address":"c@d.no"}]}}""";
        string date = DateTimeOffset.UtcNow.ToString("r");
        string hash = HmacValidator.ContentHash(Encoding.UTF8.GetBytes(body));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/emails:send?api-version=2025-09-01")
        {
            Content = new StringContent(body.Replace("c@d.no", "x@d.no"), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-ms-date", date);
        request.Headers.Add("x-ms-content-sha256", hash);
        request.Headers.TryAddWithoutValidation("Authorization", HmacValidator.Sign(AcsMockFactory.AccessKey, "POST", "/emails:send?api-version=2025-09-01", "localhost", date, hash));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("ola.nordmann@example.com", "Delivered")]
    [InlineData("bounce@example.com", "Bounced")]
    [InlineData("quarantine+1@example.com", "Quarantined")]
    [InlineData("SPAM@example.com", "FilteredSpam")]
    public async Task Send_ThatSucceeds_SendsDeliveryReportTheApiCanParse(string recipient, string expectedStatus)
    {
        var client = factory.CreateEmailClient();

        var operation = await client.SendAsync(WaitUntil.Completed, Message(recipient), TestContext.Current.CancellationToken);

        var report = await WaitForReportAsync(operation.Id);
        var eventGridEvent = EventGridEvent.Parse(BinaryData.FromString(report.Event));
        Assert.True(eventGridEvent.TryGetSystemEventData(out object data));
        var deliveryReport = Assert.IsType<AcsEmailDeliveryReportReceivedEventData>(data);
        Assert.Equal(operation.Id, deliveryReport.MessageId);
        Assert.Equal(expectedStatus, deliveryReport.Status?.ToString());
        Assert.Equal(recipient, deliveryReport.Recipient);
        Assert.Equal(Sender, deliveryReport.Sender);
    }

    [Fact]
    public async Task Send_ToNoReport_SucceedsWithoutDeliveryReport()
    {
        var client = factory.CreateEmailClient();

        var operation = await client.SendAsync(WaitUntil.Completed, Message("noreport@example.com"), TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(EmailSendStatus.Succeeded, operation.Value.Status);
        Assert.DoesNotContain(factory.DeliveryReports.Sent, r => r.Event.Contains(operation.Id));
    }

    [Theory]
    [InlineData("fail@example.com", "EmailSendFailed")]
    [InlineData("suppressed@example.com", "EmailDroppedAllRecipientsSuppressed")]
    public async Task Send_ThatFails_ThrowsWithErrorCode(string recipient, string expectedErrorCode)
    {
        var client = factory.CreateEmailClient();
        var operation = await client.SendAsync(WaitUntil.Started, Message(recipient), TestContext.Current.CancellationToken);

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => operation.UpdateStatusAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(expectedErrorCode, e.ErrorCode);
    }

    [Fact]
    public async Task Send_ToInvalid_ReturnsBadRequest()
    {
        var client = factory.CreateEmailClient();

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => client.SendAsync(WaitUntil.Started, Message("invalid@example.com"), TestContext.Current.CancellationToken));

        Assert.Equal(400, e.Status);
        Assert.Contains("Invalid format for email address", e.Message);
    }

    [Fact]
    public async Task Send_ToThrottle_Returns429Once_ThenAccepts()
    {
        var client = factory.CreateEmailClient();
        var message = Message($"throttle+{Guid.NewGuid()}@example.com");

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => client.SendAsync(WaitUntil.Started, message, TestContext.Current.CancellationToken));
        var operation = await client.SendAsync(WaitUntil.Completed, message, TestContext.Current.CancellationToken);

        Assert.Equal(429, e.Status);
        Assert.Equal("TooManyRequests", e.ErrorCode);
        Assert.Equal("10", e.GetRawResponse()?.Headers.TryGetValue("Retry-After", out string? retryAfter) == true ? retryAfter : null);
        Assert.Equal(EmailSendStatus.Succeeded, operation.Value.Status);
    }

    [Fact]
    public async Task Send_ToSlow_StaysRunning()
    {
        var client = factory.CreateEmailClient();
        var operation = await client.SendAsync(WaitUntil.Started, Message("slow@example.com"), TestContext.Current.CancellationToken);

        await operation.UpdateStatusAsync(TestContext.Current.CancellationToken);

        Assert.False(operation.HasCompleted);
    }

    [Fact]
    public async Task GetOperation_Unknown_ReturnsNotFound()
    {
        var operation = new EmailSendOperation(Guid.NewGuid().ToString(), factory.CreateEmailClient());

        var e = await Assert.ThrowsAsync<RequestFailedException>(() => operation.UpdateStatusAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(404, e.Status);
    }

    [Fact]
    public async Task UnknownPath_WithoutFallback_ReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/domains/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPath_WithFallback_IsForwardedToSimulator()
    {
        var simulator = new RecordingHandler();
        using var withFallback = factory.WithWebHostBuilder(builder => builder
            .UseSetting("AcsMockSettings:FallbackUrl", "http://localhost:5096/intercept/")
            .ConfigureTestServices(services => services.AddHttpClient(AcsEndpoints.FallbackClientName).ConfigurePrimaryHttpMessageHandler(() => simulator)));
        using var client = withFallback.CreateClient();

        using var response = await client.PostAsync("/emails:unknown?api-version=2025-09-01", new StringContent("{}", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("answered by operator", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("http://localhost:5096/intercept/acs/emails:unknown?api-version=2025-09-01", simulator.Request?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, simulator.Request?.Method);
        Assert.Equal("{}", simulator.Body);
    }

    private static EmailMessage Message(string recipient)
    {
        return new EmailMessage(Sender, recipient, new EmailContent("Subject") { PlainText = "Your code is 123456" });
    }

    private async Task<(string Event, string? TraceParent)> WaitForReportAsync(string operationId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var report = factory.DeliveryReports.Sent.FirstOrDefault(r => r.Event.Contains(operationId));
            if (report.Event is not null)
            {
                return report;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("answered by operator") };
        }
    }
}
