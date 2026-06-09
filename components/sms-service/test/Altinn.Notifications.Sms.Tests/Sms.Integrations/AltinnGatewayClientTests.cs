using System.Net;
using System.Text;

using Altinn.Notifications.Sms.Integrations.LinkMobility;

using LinkMobilityModel = LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class AltinnGatewayClientTests
{
    private static readonly SmsGatewaySettings _settings = new()
    {
        Username = "test-user",
        Password = "test-pass",
        Endpoint = "http://localhost:9999",
        TimeoutInSeconds = 30
    };

    private static readonly LinkMobilityModel.Sms _testSms = new("Altinn", "+4799999999", "Test message");

    private static AltinnGatewayClient CreateClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
        return new AltinnGatewayClient(httpClient, _settings);
    }

    [Fact]
    public async Task SendAsync_GatewayReturnsAcceptedResponse_ReturnsSuccessResult()
    {
        var xml = """
            <?xml version="1.0" encoding="ISO-8859-1"?>
            <SESSION>
              <LOGON>OK</LOGON>
              <MSGLST>
                <MSG>
                  <ID>1</ID>
                  <REF>gateway-ref-123</REF>
                  <STATUS>OK</STATUS>
                </MSG>
              </MSGLST>
            </SESSION>
            """;

        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "text/xml")
        }));

        var result = await client.SendAsync(_testSms);

        Assert.True(result.IsStatusOk);
        Assert.Equal("gateway-ref-123", result.GatewayReference);
    }

    [Fact]
    public async Task SendAsync_GatewayReturnsLogonFailure_ReturnsFailedResult()
    {
        var xml = """
            <?xml version="1.0" encoding="ISO-8859-1"?>
            <SESSION>
              <LOGON>FAIL</LOGON>
              <REASON>Invalid credentials</REASON>
            </SESSION>
            """;

        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "text/xml")
        }));

        var result = await client.SendAsync(_testSms);

        Assert.False(result.IsStatusOk);
        Assert.Equal("Invalid credentials", result.StatusText);
    }

    [Fact]
    public async Task SendAsync_GatewayReturnsNonSuccessStatusCode_ThrowsSmsGatewayException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.GatewayTimeout)));

        await Assert.ThrowsAsync<SmsGatewayException>(() => client.SendAsync(_testSms));
    }

    [Fact]
    public async Task SendAsync_RequestTimesOut_ThrowsSmsGatewayException()
    {
        var client = CreateClient(new HangingHttpMessageHandler(), timeout: TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<SmsGatewayException>(() => client.SendAsync(_testSms));
    }

    [Fact]
    public async Task SendAsync_HttpRequestExceptionThrown_ThrowsSmsGatewayException()
    {
        var client = CreateClient(new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused")));

        await Assert.ThrowsAsync<SmsGatewayException>(() => client.SendAsync(_testSms));
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class HangingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
