using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.LinkMobilityProxy;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Options;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class DeliveryReportTests
{
    [Fact]
    public async Task HttpSender_PostsXmlWithBasicAuthAndTraceParent()
    {
        using var sms = WireMockServer.Start();
        sms.Given(Request.Create().UsingPost().WithPath("/notifications/sms/api/v1/reports")).RespondWith(Response.Create().WithStatusCode(200));
        var settings = new LinkMobilityProxySettings
        {
            DeliveryReportUrl = sms.Url + "/notifications/sms/api/v1/reports",
            DeliveryReportUsername = "mock-user",
            DeliveryReportPassword = "mock-password",
        };
        using var sender = new HttpDeliveryReportSender(Options.Create(settings));

        int statusCode = await sender.SendAsync("<MSGLST/>", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", null, TestContext.Current.CancellationToken);

        Assert.Equal(200, statusCode);
        var request = Assert.Single(sms.LogEntries).RequestMessage!;
        Assert.Equal("<MSGLST/>", request.Body);
        Assert.StartsWith("application/xml", request.Headers!["Content-Type"][0]);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("mock-user:mock-password")), request.Headers["Authorization"][0]);
        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", request.Headers["traceparent"][0]);
    }

    [Fact]
    public async Task Service_SmsServiceAnswersError_RetriesUntilAccepted()
    {
        var sender = new CapturingSender();
        sender.StatusCodes.Enqueue(503);
        var logger = new ListLogger<DeliveryReportService>();
        var service = CreateService(sender, logger);
        await service.StartAsync(CancellationToken.None);

        try
        {
            service.Queue(new PendingDeliveryReport("ref-1", "+4799999999", SmsScenario.Delivered, null, null));

            await sender.WaitForReportAsync(2);
            Assert.Contains(logger.Messages, m => m.Contains("-> 503, attempt 1"));
            await logger.WaitForAsync("LinkMobility delivery report DELIVRD (Delivered) for ref-1");
            Assert.All(logger.Messages, m => Assert.DoesNotContain("+4799999999", m));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Service_ScenarioWithoutReport_SendsNothing()
    {
        var sender = new CapturingSender();
        var service = CreateService(sender, new ListLogger<DeliveryReportService>());
        await service.StartAsync(CancellationToken.None);

        try
        {
            service.Queue(new PendingDeliveryReport("ref-1", "+4799999903", SmsScenario.NoReport, null, null));

            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Empty(sender.Sent);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static DeliveryReportService CreateService(CapturingSender sender, ListLogger<DeliveryReportService> logger)
    {
        var settings = new LinkMobilityProxySettings { DeliveryReportUrl = "http://localhost/reports", DeliveryReportDelaySeconds = 0 };
        return new DeliveryReportService(sender, new SmsMirror(Options.Create(settings), new ListLogger<SmsMirror>()), Options.Create(settings), new InstantTimeProvider(), logger);
    }

    /// <summary>
    /// Skips the pause between attempts.
    /// </summary>
    private sealed class InstantTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            return System.CreateTimer(callback, state, TimeSpan.Zero, period);
        }
    }
}
