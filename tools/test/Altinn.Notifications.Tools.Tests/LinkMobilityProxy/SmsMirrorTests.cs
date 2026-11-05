using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Tools.LinkMobilityProxy;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

/// <summary>
/// The copies the proxy sends to the test harness control panel's <c>/mirror/</c>, here a WireMock server in its place.
/// </summary>
public class SmsMirrorTests
{
    [Fact]
    public async Task Send_PostsCopyAndThenDeliveryState()
    {
        using var controlPanel = StartControlPanel();
        await using var host = await ProxyHost.StartAsync(s => s.MirrorUrl = controlPanel.Url + "/mirror/");

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799999901"));
        string reference = Assert.Single(GatewayXml.ReadAccepted(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))).Reference!;

        var copy = await WaitForBodyAsync(controlPanel, "/mirror/sms");
        Assert.Equal(reference, copy.GetProperty("id").GetString());
        Assert.Equal(reference, copy.GetProperty("reference").GetString());
        Assert.Equal("Altinn", copy.GetProperty("sender").GetString());
        Assert.Equal("+4799999901", copy.GetProperty("recipient").GetString());
        Assert.Equal("Hei æøå 123456", copy.GetProperty("text").GetString());
        Assert.Equal(2880, copy.GetProperty("ttl").GetInt32());
        Assert.Equal("Undelivered", copy.GetProperty("scenario").GetString());

        var status = await WaitForBodyAsync(controlPanel, $"/mirror/sms/{reference}/status");
        Assert.Equal("Failed", status.GetProperty("status").GetString());
        Assert.Equal("UNDELIV", status.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("+4799123456", "Delivered", "DELIVRD")]
    [InlineData("+4799999902", "Expired", "EXPIRED")]
    public async Task DeliveryState_IsMappedToMirrorStatus(string recipient, string status, string state)
    {
        using var controlPanel = StartControlPanel();
        await using var host = await ProxyHost.StartAsync(s => s.MirrorUrl = controlPanel.Url + "/mirror/");

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest(recipient));

        var update = await WaitForBodyAsync(controlPanel, "/status");
        Assert.Equal(status, update.GetProperty("status").GetString());
        Assert.Equal(state, update.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RejectedRecipient_IsCopiedAsFailed()
    {
        using var controlPanel = StartControlPanel();
        await using var host = await ProxyHost.StartAsync(s => s.MirrorUrl = controlPanel.Url + "/mirror/");

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799999904"));

        var copy = await WaitForBodyAsync(controlPanel, "/mirror/sms");
        Assert.Equal("Failed", copy.GetProperty("status").GetString());
        Assert.Equal("Invalid RCV", copy.GetProperty("detail").GetString());
        Assert.Equal(JsonValueKind.Null, copy.GetProperty("reference").ValueKind);
    }

    [Fact]
    public async Task ControlPanelDown_SendAndDeliveryReportAreUnaffected()
    {
        // Nothing listens on this port.
        await using var host = await ProxyHost.StartAsync(s => s.MirrorUrl = $"http://localhost:{ProxyHost.FreePort()}/mirror/");

        using var http = new HttpClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(GatewayXml.ReadAccepted(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"The send took {stopwatch.Elapsed}.");
        Assert.Contains("<STATE>DELIVRD</STATE>", await host.Sender.WaitForReportAsync());
    }

    [Fact]
    public async Task ControlPanelHangs_SendIsNotHeldUp()
    {
        using var controlPanel = WireMockServer.Start();
        controlPanel.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(201).WithDelay(TimeSpan.FromSeconds(5)));
        await using var host = await ProxyHost.StartAsync(s => s.MirrorUrl = controlPanel.Url + "/mirror/");

        using var http = new HttpClient();
        var stopwatch = Stopwatch.StartNew();
        using var first = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));
        using var second = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123457"));
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The sends took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task WithoutMirrorUrl_NothingIsQueued()
    {
        await using var host = await ProxyHost.StartAsync();

        Assert.False(host.Mirror.IsEnabled);
        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WireMockServer StartControlPanel()
    {
        var server = WireMockServer.Start();
        server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(201));
        return server;
    }

    private static async Task<JsonElement> WaitForBodyAsync(WireMockServer server, string pathEnd)
    {
        for (int i = 0; i < 50; i++)
        {
            var entry = server.LogEntries.FirstOrDefault(e => e.RequestMessage!.Path.EndsWith(pathEnd, StringComparison.Ordinal));
            if (entry is not null)
            {
                return JsonDocument.Parse(entry.RequestMessage!.Body!).RootElement.Clone();
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Nothing posted to *{pathEnd}. Got: {string.Join(", ", server.LogEntries.Select(e => e.RequestMessage!.Path))}");
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, string body)
    {
        return http.PostAsync(url, new StringContent(body, Encoding.UTF8, "text/xml"), TestContext.Current.CancellationToken);
    }
}
