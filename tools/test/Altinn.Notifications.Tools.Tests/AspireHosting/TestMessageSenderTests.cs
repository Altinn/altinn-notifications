using System.Net.Sockets;

using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AspireHosting;

/// <summary>
/// The dashboard's Send test SMS and Send test email commands, against a WireMock server that plays both the API and the mock token issuer.
/// </summary>
public sealed class TestMessageSenderTests : IDisposable
{
    private const string TokenPath = "/api/GetEnterpriseToken";
    private const string TokenPassword = "generated-for-the-checkout";

    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _client = new();

    public void Dispose()
    {
        _client.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task SendSms_WithTokenAndOrder_ReportsTheShipment()
    {
        GivenToken(200, "token-from-mock-auth");
        GivenOrder("sms", 201, """{"notification":{"shipmentId":"5f2c"}}""");

        var result = await Sender(_server.Port).SendSmsAsync(_client, "+4799999999", "Hello", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Ordered, shipment 5f2c. It shows up in the Phone tab when sent.", result.Message);
        var tokenRequest = Assert.Single(_server.LogEntries, e => e.RequestMessage?.Path == TokenPath);
        Assert.Equal("Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"mock:{TokenPassword}")), tokenRequest.RequestMessage!.Headers!["Authorization"].Single());
        var order = Assert.Single(_server.LogEntries, e => e.RequestMessage?.Path?.EndsWith("/sms") == true);
        Assert.Equal("Bearer token-from-mock-auth", order.RequestMessage!.Headers!["Authorization"].Single());
        Assert.Contains("4799999999", order.RequestMessage!.BodyData?.BodyAsString);
    }

    [Theory]
    [InlineData("""{"notification":{}}""")]
    [InlineData("""{"other":true}""")]
    [InlineData("not json")]
    public async Task SendEmail_WithoutShipmentInTheAnswer_ReportsItAsUnknown(string answer)
    {
        GivenToken(200, "token");
        GivenOrder("email", 201, answer);

        var result = await Sender(_server.Port).SendEmailAsync(_client, "ola@example.com", "Subject", "Body", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Ordered, shipment unknown. It shows up in the Inbox tab when sent.", result.Message);
    }

    [Fact]
    public async Task Send_WhenTheTokenIsRefused_FailsWithoutOrdering()
    {
        GivenToken(401, string.Empty);

        var result = await Sender(_server.Port).SendSmsAsync(_client, "+4799999999", "Hello", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("The mock token issuer answered 401 to the token request.", result.Message);
        Assert.DoesNotContain(_server.LogEntries, e => e.RequestMessage?.Path?.Contains("/orders/") == true);
    }

    [Fact]
    public async Task Send_WhenTheApiRefusesTheOrder_FailsWithItsStatus()
    {
        GivenToken(200, "token");
        GivenOrder("sms", 400, """{"title":"One or more validation errors occurred."}""");

        var result = await Sender(_server.Port).SendSmsAsync(_client, "not a number", "Hello", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("The API answered 400.", result.Message);
    }

    [Fact]
    public async Task Send_WhenNothingAnswers_FailsWithTheReason()
    {
        var result = await Sender(FreePort()).SendSmsAsync(_client, "+4799999999", "Hello", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.StartsWith("Could not reach the API or the mock token issuer:", result.Message);
    }

    // The API and the mock token issuer as endpoints on the given port, with the address the orchestrator would give them.
    private static TestMessageSender Sender(int port)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], DisableDashboard = true });
        var api = builder.AddContainer("api", "api").WithHttpEndpoint(port: port, targetPort: port, name: "http", isProxied: false);
        var auth = builder.AddContainer("mock-auth", "mock-auth").WithHttpEndpoint(port: port, targetPort: port, name: "http", isProxied: false);
        foreach (var endpoint in builder.Resources.SelectMany(r => r.Annotations.OfType<EndpointAnnotation>()))
        {
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", port);
        }

        var password = builder.AddParameter("mock-auth-password", TokenPassword, secret: true);
        return new TestMessageSender(api.GetEndpoint("http"), auth.GetEndpoint("http"), new MockAuthCredentials("mock", password.Resource));
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private void GivenToken(int statusCode, string token)
    {
        _server.Given(Request.Create().WithPath(TokenPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBody(token));
    }

    private void GivenOrder(string channel, int statusCode, string answer)
    {
        _server.Given(Request.Create().WithPath($"/notifications/api/v1/future/orders/instant/{channel}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBody(answer));
    }
}
