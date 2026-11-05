using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Notifications.Tools.TestHarness.Configuration;

using Microsoft.AspNetCore.Mvc.Testing;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.TestHarness;

/// <summary>
/// <c>/mirror/</c>, where the mocks send copies for the Phone tab, and <c>/api/mirror/</c>, which the GUI reads.
/// </summary>
public class MirrorTests : IDisposable
{
    private const int MirrorSize = 3;

    private readonly WebApplicationFactory<TestHarnessSettings> _factory = new WebApplicationFactory<TestHarnessSettings>()
        .WithWebHostBuilder(builder => builder.UseSetting("TestHarnessSettings:MirrorSize", MirrorSize.ToString()));

    [Fact]
    public async Task Sms_IsListedNewestFirst_WithStatusSent()
    {
        var client = _factory.CreateClient();

        await PostSmsAsync(client, "ref-1", "+4799999901", "First");
        var created = await PostSmsAsync(client, "ref-2", "+4799999902", "Second");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var messages = await GetSmsAsync(client);
        Assert.Equal(["ref-2", "ref-1"], messages.Select(m => m.GetProperty("id").GetString()));

        var latest = messages[0];
        Assert.Equal("sms", latest.GetProperty("channel").GetString());
        Assert.Equal("Sent", latest.GetProperty("status").GetString());
        Assert.Equal("Altinn", latest.GetProperty("content").GetProperty("sender").GetString());
        Assert.Equal("+4799999902", latest.GetProperty("content").GetProperty("recipient").GetString());
        Assert.Equal("Second", latest.GetProperty("content").GetProperty("text").GetString());
        Assert.Equal(2880, latest.GetProperty("content").GetProperty("ttl").GetInt32());
    }

    [Fact]
    public async Task Sms_KeepsOnlyMirrorSize_DroppingTheOldest()
    {
        var client = _factory.CreateClient();

        for (int i = 1; i <= MirrorSize + 2; i++)
        {
            await PostSmsAsync(client, $"ref-{i}", "+4799999999", $"Message {i}");
        }

        var messages = await GetSmsAsync(client);
        Assert.Equal(["ref-5", "ref-4", "ref-3"], messages.Select(m => m.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task Sms_WithSameId_ReplacesTheEarlierCopy()
    {
        var client = _factory.CreateClient();

        await PostSmsAsync(client, "ref-1", "+4799999999", "First try");
        await PostSmsAsync(client, "ref-1", "+4799999999", "Second try");

        var message = Assert.Single(await GetSmsAsync(client));
        Assert.Equal("Second try", message.GetProperty("content").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Sms_WithoutIdOrReference_GetsAnId()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/mirror/sms", new { recipient = "+4799999904", status = "Failed", detail = "Invalid RCV" }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var message = Assert.Single(await GetSmsAsync(client));
        Assert.Equal(body.GetProperty("id").GetString(), message.GetProperty("id").GetString());
        Assert.Equal("Failed", message.GetProperty("status").GetString());
        Assert.Equal("Invalid RCV", message.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Status_UpdatesTheMessage()
    {
        var client = _factory.CreateClient();
        await PostSmsAsync(client, "ref-1", "+4799999901", "Hei");

        var response = await client.PostAsJsonAsync("/mirror/sms/ref-1/status", new { status = "Delivered", detail = "DELIVRD" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(await GetSmsAsync(client));
        Assert.Equal("Delivered", message.GetProperty("status").GetString());
        Assert.Equal("DELIVRD", message.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Status_ForUnknownMessage_IsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/mirror/sms/unknown/status", new { status = "Delivered" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/mirror/sms/ref-1/status", """{"status":"Bounced"}""")]
    [InlineData("/mirror/sms/ref-1/status", """{}""")]
    [InlineData("/mirror/sms", """{"text":"No recipient"}""")]
    [InlineData("/mirror/sms", """{"recipient":"+4799999999","status":"Lost"}""")]
    public async Task InvalidRequests_AreRejected(string path, string body)
    {
        var client = _factory.CreateClient();
        await PostSmsAsync(client, "ref-1", "+4799999901", "Hei");

        var response = await client.PostAsync(path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Email_IsListedWithItsBodies_AndGetsItsStatus()
    {
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/mirror/email",
            new { sender = "noreply@altinn.example", to = new[] { "ola@example.com" }, subject = "Hei", plainText = "Hei Ola", html = "<p>Hei <b>Ola</b></p>", operationId = "op-1", scenario = "Delivered" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var status = await client.PostAsJsonAsync("/mirror/email/op-1/status", new { status = "Failed", detail = "Bounced" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);

        var message = Assert.Single(await client.GetFromJsonAsync<JsonElement[]>("/api/mirror/email", TestContext.Current.CancellationToken) ?? []);
        Assert.Equal("op-1", message.GetProperty("id").GetString());
        Assert.Equal("Failed", message.GetProperty("status").GetString());
        Assert.Equal("Bounced", message.GetProperty("detail").GetString());
        var content = message.GetProperty("content");
        Assert.Equal(["ola@example.com"], content.GetProperty("to").EnumerateArray().Select(a => a.GetString()));
        Assert.Empty(content.GetProperty("cc").EnumerateArray());
        Assert.Equal("<p>Hei <b>Ola</b></p>", content.GetProperty("html").GetString());
        Assert.Equal("Hei Ola", content.GetProperty("plainText").GetString());
        Assert.Empty(await GetSmsAsync(client));
    }

    [Fact]
    public async Task Email_WithoutRecipients_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/mirror/email", new { subject = "Nobody" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownChannel_IsNotFound()
    {
        var client = _factory.CreateClient();

        var list = await client.GetAsync("/api/mirror/fax", TestContext.Current.CancellationToken);
        var status = await client.PostAsJsonAsync("/mirror/fax/1/status", new { status = "Delivered" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    [Fact]
    public async Task Clear_EmptiesTheChannel()
    {
        var client = _factory.CreateClient();
        await PostSmsAsync(client, "ref-1", "+4799999901", "Hei");

        var response = await client.DeleteAsync("/api/mirror/sms", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetSmsAsync(client));
    }

    [Fact]
    public async Task NewSms_IsAnnouncedOnTheEventStream()
    {
        var client = _factory.CreateClient();
        using var events = await client.GetStreamAsync("/api/events", TestContext.Current.CancellationToken);
        using var reader = new StreamReader(events);
        Assert.Equal("data: changed", await reader.ReadLineAsync(TestContext.Current.CancellationToken));
        await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        await PostSmsAsync(client, "ref-1", "+4799999901", "Hei");

        Assert.Equal("data: changed", await reader.ReadLineAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Task<HttpResponseMessage> PostSmsAsync(HttpClient client, string reference, string recipient, string text)
    {
        return client.PostAsJsonAsync(
            "/mirror/sms",
            new { sender = "Altinn", recipient, text, ttl = 2880, reference, scenario = "Delivered" },
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement[]> GetSmsAsync(HttpClient client)
    {
        return await client.GetFromJsonAsync<JsonElement[]>("/api/mirror/sms", TestContext.Current.CancellationToken) ?? [];
    }
}
