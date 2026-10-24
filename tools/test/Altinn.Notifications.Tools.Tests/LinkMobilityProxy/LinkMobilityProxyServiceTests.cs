using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.LinkMobilityProxy;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class LinkMobilityProxyServiceTests
{
    private const string OperatorAnswer = """<?xml version="1.0" encoding="iso-8859-1"?><SESSION><LOGON>OK</LOGON><REASON></REASON><MSGLST><MSG><ID>1</ID><STATUS>OK</STATUS><INFO></INFO><REF>operator-ref</REF></MSG></MSGLST></SESSION>""";

    [Fact]
    public async Task StubMode_AnswersOkWithReferenceAndLogsMaskedPayload()
    {
        await using var host = await ProxyHost.StartAsync();

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = Assert.Single(GatewayXml.ReadAccepted(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal("1", accepted.Id);
        Assert.True(Guid.TryParse(accepted.Reference, out _));

        var entry = await host.Logger.WaitForAsync("LinkMobility stub POST");
        Assert.Contains("(Delivered)", entry);
        Assert.Contains("<PW>***</PW>", entry);
        Assert.Contains("<RCV>+********56</RCV>", entry);
        Assert.DoesNotContain("secret", entry);
        Assert.DoesNotContain("123456", entry);
    }

    [Fact]
    public async Task StubMode_DeliveryReportContinuesTraceAndIsMaskedInLog()
    {
        await using var host = await ProxyHost.StartAsync();
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, host.Url) { Content = new StringContent(ProxyHost.SendRequest("+4799123456"), Encoding.UTF8, "text/xml") };
        request.Headers.Add("traceparent", traceParent);
        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        await host.Sender.WaitForReportAsync();
        var (report, sentTraceParent) = Assert.Single(host.Sender.Sent);
        Assert.Equal(traceParent, sentTraceParent);
        Assert.Contains("<STATE>DELIVRD</STATE>", report);
        Assert.Contains("<RCV>+4799123456</RCV>", report);
    }

    [Fact]
    public async Task StubMode_WithoutDeliveryReportUrl_SendsNoReports()
    {
        await using var host = await ProxyHost.StartAsync(s => s.DeliveryReportUrl = string.Empty);

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(host.Sender.Sent);
    }

    [Fact]
    public async Task StubMode_UnknownPathWithoutFallback_Is404()
    {
        await using var host = await ProxyHost.StartAsync();

        using var http = new HttpClient();
        using var response = await http.GetAsync(host.Url + "/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StubMode_UnknownRequestWithFallback_GoesToLinkMobilityPathOfFallback()
    {
        using var simulator = WireMockServer.Start();
        simulator.Given(Request.Create().UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(418).WithBody("from simulator"));
        await using var host = await ProxyHost.StartAsync(s => s.FallbackUrl = simulator.Url + "/intercept/");

        using var http = new HttpClient();
        using var unknownPath = await http.GetAsync(host.Url + "/unknown?x=1", TestContext.Current.CancellationToken);
        using var notASend = await PostAsync(http, host.Url, "<NOT-A-SESSION/>");

        Assert.Equal((HttpStatusCode)418, unknownPath.StatusCode);
        Assert.Equal("from simulator", await unknownPath.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal((HttpStatusCode)418, notASend.StatusCode);
        Assert.Contains(simulator.LogEntries, e => e.RequestMessage!.Path == "/intercept/linkmobility/unknown" && e.RequestMessage.RawQuery == "?x=1");
        Assert.Contains(simulator.LogEntries, e => e.RequestMessage!.Path == "/intercept/linkmobility/" && e.RequestMessage.Body == "<NOT-A-SESSION/>");
    }

    [Fact]
    public async Task InteractiveMode_SendGoesToFallbackAndOperatorReferenceGetsReport()
    {
        using var simulator = WireMockServer.Start();
        simulator.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "text/xml").WithBody(OperatorAnswer));
        await using var host = await ProxyHost.StartAsync(s =>
        {
            s.FallbackUrl = simulator.Url + "/intercept/";
            s.Interactive = true;
        });

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799999901"));

        Assert.Equal(OperatorAnswer, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProxyHost.SendRequest("+4799999901"), Assert.Single(simulator.LogEntries).RequestMessage!.Body);

        string report = await host.Sender.WaitForReportAsync();
        Assert.Contains("<REF>operator-ref</REF>", report);
        Assert.Contains("<STATE>UNDELIV</STATE>", report);
        await host.Logger.WaitForAsync("LinkMobility interactive POST / -> 200 (Undelivered)");
    }

    [Fact]
    public async Task InteractiveMode_OperatorRejects_SendsNoReport()
    {
        using var simulator = WireMockServer.Start();
        simulator.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(200).WithBody(
            """<SESSION><LOGON>OK</LOGON><MSGLST><MSG><ID>1</ID><STATUS>FAIL</STATUS><INFO>Invalid RCV</INFO></MSG></MSGLST></SESSION>"""));
        await using var host = await ProxyHost.StartAsync(s =>
        {
            s.FallbackUrl = simulator.Url + "/intercept/";
            s.Interactive = true;
        });

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799999999"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(host.Sender.Sent);
    }

    [Fact]
    public async Task InteractiveMode_WithoutFallback_FailsToStart()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProxyHost.StartAsync(s => s.Interactive = true));
    }

    [Fact]
    public async Task ProxyMode_ForwardsUnmaskedPayloadButLogsMaskedPayload()
    {
        using var gateway = WireMockServer.Start();
        gateway.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(200).WithBody("<SESSION><LOGON>OK</LOGON></SESSION>"));
        await using var host = await ProxyHost.StartAsync(s => s.Target = gateway.Url!);

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, ProxyHost.SendRequest("+4799123456"));

        Assert.Equal("<SESSION><LOGON>OK</LOGON></SESSION>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProxyHost.SendRequest("+4799123456"), Assert.Single(gateway.LogEntries).RequestMessage!.Body);

        var entry = await host.Logger.WaitForAsync("LinkMobility proxy POST");
        Assert.Contains("<PW>***</PW>", entry);
        Assert.DoesNotContain("secret", entry);

        // The real gateway sends its reports to the endpoint registered for the account, not to the proxy.
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(host.Sender.Sent);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, string body)
    {
        return http.PostAsync(url, new StringContent(body, Encoding.UTF8, "text/xml"), TestContext.Current.CancellationToken);
    }
}
