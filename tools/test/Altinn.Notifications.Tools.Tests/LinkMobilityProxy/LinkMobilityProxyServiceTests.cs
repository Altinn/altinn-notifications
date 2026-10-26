using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.LinkMobilityProxy;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

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

    [Fact]
    public async Task StubMode_Plain_LogsTextEscapedAndBytesWithOnlyPasswordMasked()
    {
        await using var host = await ProxyHost.StartAsync(s => s.LogPayloads = PayloadLogMode.Plain);

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, OddSendRequest("+4799123456"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = await host.Logger.WaitForAsync("LinkMobility stub POST / -> 200 (Delivered)");
        Assert.Contains("<PW>***</PW>", entry);
        Assert.Contains("<RCV>+4799123456</RCV>", entry);
        Assert.Contains("<TEXT>Kode 123456\r\næøå", entry);
        Assert.Contains(@"Escaped: <?xml version=""1.0"" encoding=""iso-8859-1""?><SESSION><CLIENT>user</CLIENT><PW>***</PW>", entry);
        Assert.Contains(@"<TEXT>Kode 123456\r\næøå[U+00A0]a[U+FFFD]</TEXT>", entry);

        // 0D 0A is \r\n, C3 A6 C3 B8 C3 A5 æøå in UTF-8, C2 A0 the NBSP, and FF the invalid byte.
        Assert.Contains("0D 0A C3 A6 C3 B8 C3 A5 C2 A0 61 FF 3C 2F 54 45 58 54 3E", entry);
        Assert.Contains("Response: text/xml, ", entry);
        Assert.DoesNotContain("secret", entry);
        Assert.DoesNotContain(PlainPayload.Hex("secret"u8.ToArray()), entry);
    }

    [Fact]
    public async Task StubMode_Plain_ControlCharacterXmlDoesNotAllow_IsLoggedThoughTheStubRejectsTheSend()
    {
        await using var host = await ProxyHost.StartAsync(s => s.LogPayloads = PayloadLogMode.Plain);

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, OddSendRequest("+4799123456", "\u0007"));

        // XML 1.0 does not allow a raw bell, so the stub cannot read the send.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var entry = await host.Logger.WaitForAsync("LinkMobility stub POST / -> 404 (-)");
        Assert.Contains("<TEXT>Kode 123456\r\n\u0007æøå", entry);
        Assert.Contains(@"<TEXT>Kode 123456\r\n\u0007æøå[U+00A0]a[U+FFFD]</TEXT>", entry);
        Assert.Contains("0D 0A 07 C3 A6", entry);
        Assert.DoesNotContain("secret", entry);
    }

    [Fact]
    public async Task StubMode_PlainWithPayloadFolder_WritesByteExactFilesWithOnlyPasswordMasked()
    {
        string folder = Path.Combine(Path.GetTempPath(), "linkmobility-proxy-tests", Guid.NewGuid().ToString());
        try
        {
            await using var host = await ProxyHost.StartAsync(s =>
            {
                s.LogPayloads = PayloadLogMode.Plain;
                s.PayloadFolder = folder;
            });

            using var http = new HttpClient();
            using var response = await PostAsync(http, host.Url, OddSendRequest("+4799123456"));
            string reference = Assert.Single(GatewayXml.ReadAccepted(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))).Reference!;

            var entry = await host.Logger.WaitForAsync("LinkMobility stub POST");
            string requestFile = Assert.Single(Directory.GetFiles(folder, "*-request.xml"));
            string responseFile = Assert.Single(Directory.GetFiles(folder, "*-response.xml"));
            Assert.EndsWith($"-0001-ref-{reference}-request.xml", requestFile);
            Assert.Contains(requestFile, entry);
            Assert.Equal(PayloadMasker.MaskPassword(OddSendRequest("+4799123456")), await File.ReadAllBytesAsync(requestFile, TestContext.Current.CancellationToken));
            Assert.Contains($"<REF>{reference}</REF>", await File.ReadAllTextAsync(responseFile, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StubMode_Plain_WarnsAtStartup()
    {
        await using var host = await ProxyHost.StartAsync(s => s.LogPayloads = PayloadLogMode.Plain);

        Assert.Contains(host.Logger.Messages, m => m.StartsWith("LinkMobility proxy logs payloads in plain text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProxyMode_Plain_ForwardsBytesUnchangedAndLogsThem()
    {
        using var gateway = WireMockServer.Start();
        gateway.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(200).WithBody("<SESSION><LOGON>OK</LOGON></SESSION>"));
        await using var host = await ProxyHost.StartAsync(s =>
        {
            s.Target = gateway.Url!;
            s.LogPayloads = PayloadLogMode.Plain;
        });

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, OddSendRequest("+4799123456", "\u0007"));

        Assert.Equal(OddSendRequest("+4799123456", "\u0007"), Assert.Single(gateway.LogEntries).RequestMessage!.BodyAsBytes);
        var entry = await host.Logger.WaitForAsync("LinkMobility proxy POST");
        Assert.Contains("<RCV>+4799123456</RCV>", entry);
        Assert.Contains(@"\u0007æøå[U+00A0]a[U+FFFD]", entry);
        Assert.Contains("61 FF 3C", entry);
        Assert.Contains("<SESSION><LOGON>OK</LOGON></SESSION>", entry);
        Assert.DoesNotContain("secret", entry);
    }

    [Fact]
    public async Task Masked_IsTheDefault()
    {
        await using var host = await ProxyHost.StartAsync();

        using var http = new HttpClient();
        using var response = await PostAsync(http, host.Url, OddSendRequest("+4799123456"));

        Assert.Equal(PayloadLogMode.Masked, host.Settings.LogPayloads);
        var entry = await host.Logger.WaitForAsync("LinkMobility stub POST");
        Assert.Contains("<TEXT>[masked, ", entry);
        Assert.DoesNotContain("Escaped:", entry);
        Assert.DoesNotContain("+4799123456", entry);
    }

    [Theory]
    [InlineData("http://localhost:5093")]
    [InlineData("http://127.0.0.1:5093")]
    [InlineData("http://[::1]:5093")]
    public void EnsurePayloadLoggingIsLocal_PlainOnLoopback_IsAllowed(string url)
    {
        LinkMobilityProxyService.EnsurePayloadLoggingIsLocal(new LinkMobilityProxySettings { LogPayloads = PayloadLogMode.Plain }, ["http://localhost:5093", url]);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5093")]
    [InlineData("http://*:5093")]
    [InlineData("http://+:5093")]
    [InlineData("http://[::]:5093")]
    [InlineData("http://linkmobility-proxy:5093")]
    public void EnsurePayloadLoggingIsLocal_PlainNotOnLoopbackOnly_Throws(string url)
    {
        var settings = new LinkMobilityProxySettings { LogPayloads = PayloadLogMode.Plain };

        var ex = Assert.Throws<InvalidOperationException>(() => LinkMobilityProxyService.EnsurePayloadLoggingIsLocal(settings, ["http://localhost:5093", url]));
        Assert.Contains(url, ex.Message);

        // Masked logging is fine anywhere.
        LinkMobilityProxyService.EnsurePayloadLoggingIsLocal(new LinkMobilityProxySettings(), [url]);
    }

    [Fact]
    public async Task PayloadFolderWithoutPlain_FailsToStart()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProxyHost.StartAsync(s => s.PayloadFolder = Path.GetTempPath()));
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, string body)
    {
        return http.PostAsync(url, new StringContent(body, Encoding.UTF8, "text/xml"), TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");
        return http.PostAsync(url, content, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A send whose text has \r\n, <paramref name="extra"/>, æøå, a non-breaking space and an invalid UTF-8 byte, as the bytes
    /// LinkMobility would get.
    /// </summary>
    private static byte[] OddSendRequest(string recipient, string extra = "")
    {
        string before = """<?xml version="1.0" encoding="iso-8859-1"?><SESSION><CLIENT>user</CLIENT><PW>secret</PW><MSGLST><MSG><ID>1</ID><TEXT>Kode 123456""" + "\r\n" + extra + "æøå\u00A0a";
        string after = $"""</TEXT><SND>Altinn</SND><RCV>{recipient}</RCV><TTL>2880</TTL></MSG></MSGLST></SESSION>""";
        return [.. Encoding.UTF8.GetBytes(before), 0xFF, .. Encoding.UTF8.GetBytes(after)];
    }
}
