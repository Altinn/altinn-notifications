using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.Simulator.Configuration;
using Altinn.Notifications.Tools.Tests.AcsMock;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.Simulator;

public class SimulatorTests(SimulatorFactory factory) : IClassFixture<SimulatorFactory>
{
    [Fact]
    public async Task HeldRequest_AnsweredThroughApi_ReachesCaller()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/intercept/profile/profile/api/v1/unknown?x=1")
        {
            Content = new StringContent("""{"nationalIdentityNumbers":["12345678901"]}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", "Bearer secret-token");
        request.Headers.Add("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        var call = client.SendAsync(request, TestContext.Current.CancellationToken);
        var held = await WaitForPendingAsync(client, "/profile/api/v1/unknown?x=1");

        Assert.Equal("profile", held.GetProperty("system").GetString());
        Assert.Equal("POST", held.GetProperty("method").GetString());
        Assert.Equal("Bearer ***", held.GetProperty("headers").GetProperty("Authorization").GetString());
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", held.GetProperty("traceId").GetString());
        Assert.Contains("12345678901", held.GetProperty("body").GetString());

        var answer = await client.PostAsJsonAsync(
            $"/api/requests/{held.GetProperty("id").GetString()}/answer",
            new { status = 201, headers = new Dictionary<string, string> { ["Content-Type"] = "application/json", ["X-Test"] = "yes" }, body = """{"answered":true}""" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);

        var response = await call;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("yes", response.Headers.GetValues("X-Test").Single());
        Assert.Equal("operator", response.Headers.GetValues("X-Simulator").Single());
        Assert.Equal("""{"answered":true}""", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var entry = await HistoryEntryAsync(client, held.GetProperty("id").GetString()!);
        Assert.Equal("Answered", entry.GetProperty("outcome").GetString());
        Assert.Equal(201, entry.GetProperty("response").GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task HeldRequest_WithoutAnswer_TimesOutWith504()
    {
        using var shortTimeout = factory.WithWebHostBuilder(builder => builder.UseSetting("SimulatorSettings:TimeoutSeconds", "1"));
        var client = shortTimeout.CreateClient();

        var response = await client.GetAsync("/intercept/register/register/api/v1/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("timeout", response.Headers.GetValues("X-Simulator").Single());

        var state = await GetStateAsync(client);
        var entry = state.GetProperty("history").EnumerateArray().Single(h => h.GetProperty("request").GetProperty("path").GetString() == "/register/api/v1/unknown");
        Assert.Equal("TimedOut", entry.GetProperty("outcome").GetString());
        Assert.Empty(state.GetProperty("pending").EnumerateArray());

        // Too late: the request is no longer held.
        var late = await client.PostAsJsonAsync(
            $"/api/requests/{entry.GetProperty("request").GetProperty("id").GetString()}/answer",
            new { status = 200 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, late.StatusCode);
    }

    [Fact]
    public async Task Answer_WithHeaderOnSeveralLines_IsRejected()
    {
        var client = factory.CreateClient();
        var call = client.GetAsync("/intercept/acs/unknown-header-test", TestContext.Current.CancellationToken);
        var held = await WaitForPendingAsync(client, "/unknown-header-test");
        string id = held.GetProperty("id").GetString()!;

        var rejected = await client.PostAsJsonAsync($"/api/requests/{id}/answer", new { status = 200, headers = new Dictionary<string, string> { ["X-Bad"] = "a\r\nb" } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // Still held, so a valid answer goes through.
        await client.PostAsJsonAsync($"/api/requests/{id}/answer", new { status = 204 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, (await call).StatusCode);
    }

    [Fact]
    public async Task Templates_LoadForEverySystem()
    {
        var client = factory.CreateClient();

        var templates = await client.GetFromJsonAsync<JsonElement>("/api/templates", TestContext.Current.CancellationToken);
        var byKey = templates.EnumerateArray().ToDictionary(t => t.GetProperty("key").GetString()!);

        string[] expected =
        [
            "acs/202-accepted", "acs/400-bad-request", "acs/401-denied", "acs/429-throttled",
            "profile/200-empty", "profile/404-not-found", "profile/500-error",
            "register/200-empty", "register/404-not-found", "register/500-error",
            "authorization/permit", "authorization/deny",
            "conditions/true", "conditions/false", "conditions/corrupt",
            "linkmobility/ok", "linkmobility/invalid-rcv", "linkmobility/logon-fail",
            "generic/200-empty", "generic/404-not-found", "generic/500-error", "generic/503-unavailable",
        ];
        Assert.All(expected, key => Assert.Contains(key, byKey.Keys));

        Assert.Equal(429, byKey["acs/429-throttled"].GetProperty("status").GetInt32());
        Assert.Equal("10", byKey["acs/429-throttled"].GetProperty("headers").GetProperty("Retry-After").GetString());

        // {{guid}} is filled in with the same id in the headers and the body.
        var accepted = byKey["acs/202-accepted"];
        string operationId = accepted.GetProperty("headers").GetProperty("operation-id").GetString()!;
        Assert.True(Guid.TryParse(operationId, out _));
        Assert.Contains(operationId, accepted.GetProperty("body").GetString());
        Assert.Contains("<REF>", byKey["linkmobility/ok"].GetProperty("body").GetString());
        Assert.DoesNotContain("{{guid}}", byKey["linkmobility/ok"].GetProperty("body").GetString());

        // A system's own templates come first, then the generic ones.
        var forConditions = await client.GetFromJsonAsync<JsonElement>("/api/templates?system=conditions", TestContext.Current.CancellationToken);
        Assert.Equal(["conditions", "conditions", "conditions", "generic", "generic", "generic", "generic"], forConditions.EnumerateArray().Select(t => t.GetProperty("system").GetString()));
    }

    [Fact]
    public async Task AutoReply_AnswersWithoutHolding()
    {
        // A host of its own, so the auto-reply does not answer the other tests' requests.
        using var host = factory.WithWebHostBuilder(_ => { });
        var client = host.CreateClient();
        var set = await client.PutAsJsonAsync("/api/auto-replies/conditions", new { template = "conditions/false" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var response = await client.GetAsync("/intercept/conditions/condition/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("auto", response.Headers.GetValues("X-Simulator").Single());
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("sendNotification").GetBoolean());

        var unknownTemplate = await client.PutAsJsonAsync("/api/auto-replies/conditions", new { template = "conditions/nope" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownTemplate.StatusCode);
    }

    [Fact]
    public async Task LinkMobilityPassword_IsMaskedInTheGui()
    {
        var client = factory.CreateClient();
        var content = new StringContent("<SESSION><CLIENT>user</CLIENT><PW>secret</PW></SESSION>", Encoding.Latin1, "text/xml");
        content.Headers.ContentType!.CharSet = "iso-8859-1";
        var call = client.PostAsync("/intercept/linkmobility/masking-test", content, TestContext.Current.CancellationToken);

        var held = await WaitForPendingAsync(client, "/masking-test");

        Assert.Equal("<SESSION><CLIENT>user</CLIENT><PW>***</PW></SESSION>", held.GetProperty("body").GetString());
        await client.PostAsJsonAsync($"/api/requests/{held.GetProperty("id").GetString()}/answer", new { status = 200 }, TestContext.Current.CancellationToken);
        await call;
    }

    /// <summary>
    /// The contract with the mocks: the ACS mock forwards an unknown request to <c>{FallbackUrl}acs/{path}</c>, and the operator's answer comes back through it.
    /// </summary>
    [Fact]
    public async Task AcsMockFallback_ReachesSimulator_AndAnswerReachesCaller()
    {
        var simulator = factory.CreateClient();
        using var acs = new AcsMockFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AcsMockSettings:FallbackUrl", "http://localhost/intercept/");
            builder.ConfigureTestServices(services => services
                .AddHttpClient(Altinn.Notifications.Tools.AcsMock.AcsEndpoints.FallbackClientName)
                .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler()));
        });

        var call = acs.CreateClient().PostAsync("/emails:unknown?api-version=2025-09-01", new StringContent("{}", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        var held = await WaitForPendingAsync(simulator, "/emails:unknown?api-version=2025-09-01");
        Assert.Equal("acs", held.GetProperty("system").GetString());

        await simulator.PostAsJsonAsync(
            $"/api/requests/{held.GetProperty("id").GetString()}/answer",
            new { status = 429, headers = new Dictionary<string, string> { ["Retry-After"] = "7" }, body = string.Empty },
            TestContext.Current.CancellationToken);

        var response = await call;
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("7", response.Headers.GetValues("Retry-After").Single());
    }

    private static async Task<JsonElement> GetStateAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/state", TestContext.Current.CancellationToken);

    private static async Task<JsonElement> WaitForPendingAsync(HttpClient client, string path)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var state = await GetStateAsync(client);
            foreach (var request in state.GetProperty("pending").EnumerateArray())
            {
                if (request.GetProperty("path").GetString() == path)
                {
                    return request;
                }
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No pending request for {path}. Pending: {(await GetStateAsync(client)).GetProperty("pending")}");
    }

    private static async Task<JsonElement> HistoryEntryAsync(HttpClient client, string id)
    {
        var state = await GetStateAsync(client);
        return state.GetProperty("history").EnumerateArray().Single(h => h.GetProperty("request").GetProperty("id").GetString() == id);
    }
}
