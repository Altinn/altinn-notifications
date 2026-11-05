using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

using Altinn.Notifications.Tools.AcsMock.Configuration;

using Azure;
using Azure.Communication.Email;
using Azure.Core.Pipeline;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using WireMock.RequestBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

/// <summary>
/// The copies the mock sends to the test harness control panel's <c>/mirror/</c>, here a WireMock server in its place.
/// </summary>
public sealed class EmailMirrorTests : IDisposable
{
    private const string Sender = "noreply@altinn.example";

    private readonly AcsMockFactory _factory = new();
    private readonly WireMockServer _controlPanel = WireMockServer.Start();

    public EmailMirrorTests()
    {
        _controlPanel.Given(Request.Create().UsingPost()).RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(201));
    }

    [Fact]
    public async Task Send_PostsCopyAndThenDeliveredStatus()
    {
        var client = CreateEmailClient(_controlPanel.Url + "/mirror/");
        var message = new EmailMessage(Sender, "ola.nordmann@example.com", new EmailContent("Your code") { PlainText = "Your code is 123456", Html = "<p>Your code is <b>123456</b></p>" });
        message.Recipients.CC.Add(new EmailAddress("kari@example.com"));

        var operation = await client.SendAsync(WaitUntil.Started, message, TestContext.Current.CancellationToken);
        await operation.UpdateStatusAsync(TestContext.Current.CancellationToken);

        var copy = await WaitForBodyAsync("/mirror/email");
        Assert.Equal(operation.Id, copy.GetProperty("id").GetString());
        Assert.Equal(operation.Id, copy.GetProperty("operationId").GetString());
        Assert.Equal(Sender, copy.GetProperty("sender").GetString());
        Assert.Equal(["ola.nordmann@example.com"], copy.GetProperty("to").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(["kari@example.com"], copy.GetProperty("cc").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("Your code", copy.GetProperty("subject").GetString());
        Assert.Equal("Your code is 123456", copy.GetProperty("plainText").GetString());
        Assert.Equal("<p>Your code is <b>123456</b></p>", copy.GetProperty("html").GetString());
        Assert.Equal("Delivered", copy.GetProperty("scenario").GetString());

        var status = await WaitForBodyAsync($"/mirror/email/{operation.Id}/status");
        Assert.Equal("Delivered", status.GetProperty("status").GetString());
        Assert.Equal("Delivered", status.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("bounce@example.com", "Bounced")]
    [InlineData("fail@example.com", "EmailSendFailed")]
    [InlineData("suppressed@example.com", "EmailDroppedAllRecipientsSuppressed")]
    public async Task Failure_IsPostedAsFailed(string recipient, string detail)
    {
        var client = CreateEmailClient(_controlPanel.Url + "/mirror/");

        var operation = await client.SendAsync(WaitUntil.Started, Message(recipient), TestContext.Current.CancellationToken);
        try
        {
            await operation.UpdateStatusAsync(TestContext.Current.CancellationToken);
        }
        catch (RequestFailedException)
        {
            // The SDK throws for an operation that ended Failed.
        }

        var status = await WaitForBodyAsync($"/mirror/email/{operation.Id}/status");
        Assert.Equal("Failed", status.GetProperty("status").GetString());
        Assert.Equal(detail, status.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvalidAddress_IsCopiedAsFailed()
    {
        var client = CreateEmailClient(_controlPanel.Url + "/mirror/");

        await Assert.ThrowsAsync<RequestFailedException>(() => client.SendAsync(WaitUntil.Started, Message("invalid@example.com"), TestContext.Current.CancellationToken));

        var copy = await WaitForBodyAsync("/mirror/email");
        Assert.Equal("Failed", copy.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, copy.GetProperty("operationId").ValueKind);
    }

    [Fact]
    public async Task ControlPanelDown_SendIsUnaffected()
    {
        var client = CreateEmailClient($"http://localhost:{FreePort()}/mirror/");

        var stopwatch = Stopwatch.StartNew();
        var operation = await client.SendAsync(WaitUntil.Started, Message("ola.nordmann@example.com"), TestContext.Current.CancellationToken);
        await operation.UpdateStatusAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(EmailSendStatus.Succeeded, operation.Value.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"The send took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ControlPanelHangs_SendIsNotHeldUp()
    {
        using var slow = WireMockServer.Start();
        slow.Given(Request.Create().UsingPost()).RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(201).WithDelay(TimeSpan.FromSeconds(5)));
        var client = CreateEmailClient(slow.Url + "/mirror/");

        var stopwatch = Stopwatch.StartNew();
        await client.SendAsync(WaitUntil.Started, Message("ola.nordmann@example.com"), TestContext.Current.CancellationToken);
        await client.SendAsync(WaitUntil.Started, Message("kari.nordmann@example.com"), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The sends took {stopwatch.Elapsed}.");
    }

    public void Dispose()
    {
        _factory.Dispose();
        _controlPanel.Dispose();
    }

    private static EmailMessage Message(string recipient)
    {
        return new EmailMessage(Sender, recipient, new EmailContent("Subject") { PlainText = "Hei" });
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private EmailClient CreateEmailClient(string mirrorUrl)
    {
        WebApplicationFactory<AcsMockSettings> mock = _factory.WithWebHostBuilder(builder => builder.UseSetting("AcsMockSettings:MirrorUrl", mirrorUrl));
        var options = new EmailClientOptions { Transport = new HttpClientTransport(mock.Server.CreateHandler()) };
        options.Retry.MaxRetries = 0;
        return new EmailClient($"endpoint=http://localhost/;accesskey={AcsMockFactory.AccessKey}", options);
    }

    private async Task<JsonElement> WaitForBodyAsync(string path)
    {
        for (int i = 0; i < 50; i++)
        {
            var entry = _controlPanel.LogEntries.FirstOrDefault(e => e.RequestMessage!.Path == path);
            if (entry is not null)
            {
                return JsonDocument.Parse(entry.RequestMessage!.Body!).RootElement.Clone();
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Nothing posted to {path}. Got: {string.Join(", ", _controlPanel.LogEntries.Select(e => e.RequestMessage!.Path))}");
    }
}
