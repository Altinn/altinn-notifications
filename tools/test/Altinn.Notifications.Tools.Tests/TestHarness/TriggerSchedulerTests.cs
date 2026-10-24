using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.TestHarness.Triggers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.TestHarness;

public class TriggerSchedulerTests
{
    private static readonly string[] _allJobs = [.. TriggerCatalog.All.Select(d => d.Name)];

    [Fact]
    public async Task RunNow_PostsJsonToTriggerEndpoint_AndRecordsRun()
    {
        var api = new FakeApi((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("done") }));
        using var factory = CreateFactory(api, ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/triggers/sendsmsanytime/run", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(200, run.GetProperty("status").GetInt32());
        Assert.Equal("manual", run.GetProperty("source").GetString());
        Assert.Equal("done", run.GetProperty("response").GetString());

        var call = Assert.Single(api.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("http://api.test/notifications/api/v1/trigger/sendsmsanytime", call.Url);
        Assert.Equal("application/json", call.ContentType);

        var job = await GetJobAsync(client, "sendsmsanytime");
        Assert.False(job.GetProperty("enabled").GetBoolean());
        Assert.Equal(200, job.GetProperty("lastRun").GetProperty("status").GetInt32());
        Assert.Single(job.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task EnabledJob_RunsOnItsOwn_AndPausedJobsDoNot()
    {
        var api = new FakeApi();
        using var factory = CreateFactory(api, OnlyEnabled("sendemail", intervalSeconds: 1));
        var client = factory.CreateClient();

        await WaitUntilAsync(() => api.Calls.Count(c => c.Url.EndsWith("/sendemail")) >= 2);

        Assert.All(api.Calls, c => Assert.EndsWith("/sendemail", c.Url));
        var job = await GetJobAsync(client, "sendemail");
        Assert.Equal("scheduled", job.GetProperty("lastRun").GetProperty("source").GetString());
        Assert.NotEqual(JsonValueKind.Null, job.GetProperty("nextRunAt").ValueKind);
    }

    [Fact]
    public async Task PausedJob_ResumedThroughApi_StartsRunning()
    {
        var api = new FakeApi();
        using var factory = CreateFactory(api, ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/triggers/terminateexpirednotifications", new { enabled = true, intervalSeconds = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await WaitUntilAsync(() => api.Calls.Any(c => c.Url.EndsWith("/terminateexpirednotifications")));

        var pause = await client.PutAsJsonAsync("/api/triggers", new { enabled = false }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, pause.StatusCode);
        var jobs = (await client.GetFromJsonAsync<JsonElement>("/api/triggers", TestContext.Current.CancellationToken)).GetProperty("jobs");
        Assert.All(jobs.EnumerateArray(), j => Assert.False(j.GetProperty("enabled").GetBoolean()));
    }

    [Fact]
    public async Task JobOutsideItsHours_IsNotScheduled_ButCanRunByHand()
    {
        int otherHour = (DateTime.UtcNow.Hour + 12) % 24;
        var api = new FakeApi();
        using var factory = CreateFactory(api, [.. OnlyEnabled("sendsms", intervalSeconds: 1), ("TriggerSettings:Jobs:sendsms:Hours", $"{otherHour}")]);
        var client = factory.CreateClient();

        var job = await GetJobAsync(client, "sendsms");
        Assert.True(job.GetProperty("outsideHours").GetBoolean());
        Assert.Equal($"{otherHour}", job.GetProperty("hours").GetString());

        await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);
        Assert.Empty(api.Calls);

        var run = await client.PostAsync("/api/triggers/sendsms/run", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task ApiUnreachable_RunHasNoStatus_AndTheError()
    {
        var api = new FakeApi((_, _) => throw new HttpRequestException("Connection refused (api.test:80)"));
        using var factory = CreateFactory(api, ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var run = await client.PostAsync("/api/triggers/sendemail/run", null, TestContext.Current.CancellationToken);

        var body = await run.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("status").ValueKind);
        Assert.False(body.GetProperty("succeeded").GetBoolean());
        Assert.Contains("Connection refused", body.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ApiError_IsRecordedWithStatusAndBody()
    {
        var api = new FakeApi((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Failed to terminate expired notifications") }));
        using var factory = CreateFactory(api, ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        await client.PostAsync("/api/triggers/terminateexpirednotifications/run", null, TestContext.Current.CancellationToken);

        var last = (await GetJobAsync(client, "terminateexpirednotifications")).GetProperty("lastRun");
        Assert.Equal(500, last.GetProperty("status").GetInt32());
        Assert.Equal("Failed to terminate expired notifications", last.GetProperty("response").GetString());
    }

    [Fact]
    public async Task RunNow_WhileRunning_IsConflict()
    {
        var release = new TaskCompletionSource();
        var api = new FakeApi(async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var factory = CreateFactory(api, ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var first = client.PostAsync("/api/triggers/sendemail/run", null, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => api.Calls.Count == 1);
        Assert.True((await GetJobAsync(client, "sendemail")).GetProperty("running").GetBoolean());

        var second = await client.PostAsync("/api/triggers/sendemail/run", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        release.SetResult();
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task Update_WithBadHoursOrInterval_IsValidationProblem()
    {
        using var factory = CreateFactory(new FakeApi(), ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/triggers/sendsms", new { intervalSeconds = 0, hours = "7-25" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("intervalSeconds", out _));
        Assert.True(errors.TryGetProperty("hours", out _));
    }

    [Fact]
    public async Task UnknownTrigger_IsNotFound()
    {
        using var factory = CreateFactory(new FakeApi(), ("TriggerSettings:Enabled", "false"));
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/triggers/sendfax/run", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Defaults_FollowTheClusterSchedules()
    {
        using var factory = CreateFactory(new FakeApi(), ("TriggerSettings:Jobs:sendemail:IntervalSeconds", "30"));
        var client = factory.CreateClient();

        var jobs = (await client.GetFromJsonAsync<JsonElement>("/api/triggers", TestContext.Current.CancellationToken)).GetProperty("jobs").EnumerateArray()
            .ToDictionary(j => j.GetProperty("name").GetString()!);

        Assert.Equal(_allJobs, jobs.Keys);
        Assert.Equal(30, jobs["sendemail"].GetProperty("intervalSeconds").GetInt32());
        Assert.Equal("7-16", jobs["sendsms"].GetProperty("hours").GetString());
        Assert.Equal("* 7-16 * * *", jobs["sendsms"].GetProperty("clusterSchedule").GetString());
        Assert.True(jobs["sendsmsanytime"].GetProperty("enabled").GetBoolean());
        Assert.True(jobs["terminateexpirednotifications"].GetProperty("enabled").GetBoolean());
        Assert.False(jobs["deleteoldstatusfeedrecords"].GetProperty("enabled").GetBoolean());
        Assert.False(jobs["pastdueoneorder"].GetProperty("enabled").GetBoolean());
    }

    [Theory]
    [InlineData("", 24, "")]
    [InlineData("*", 24, "")]
    [InlineData("7-16", 10, "7-16")]
    [InlineData("22-2", 5, "0-2,22-23")]
    [InlineData("3, 5-6", 3, "3,5-6")]
    public void HourRange_ParsesCronHourField(string text, int count, string formatted)
    {
        Assert.True(HourRange.TryParse(text, out bool[] hours));
        Assert.Equal(count, hours.Count(h => h));
        Assert.Equal(formatted, HourRange.Format(hours));
    }

    [Theory]
    [InlineData("24")]
    [InlineData("7-")]
    [InlineData("a-b")]
    [InlineData("1-2-3")]
    public void HourRange_RejectsWhatIsNotAnHourField(string text)
    {
        Assert.False(HourRange.TryParse(text, out _));
    }

    private static WebApplicationFactory<Tools.TestHarness.Configuration.TestHarnessSettings> CreateFactory(FakeApi api, params (string Key, string Value)[] settings) =>
        new TestHarnessFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TriggerSettings:ApiBaseUrl", "http://api.test");
            builder.UseSetting("TriggerSettings:Enabled", "true");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.AddHttpClient(TriggerScheduler.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => api));
        });

    private static (string Key, string Value)[] OnlyEnabled(string name, int intervalSeconds) =>
    [
        .. _allJobs.Select(job => ($"TriggerSettings:Jobs:{job}:Enabled", (job == name).ToString())),
        ($"TriggerSettings:Jobs:{name}:IntervalSeconds", intervalSeconds.ToString()),
    ];

    private static async Task<JsonElement> GetJobAsync(HttpClient client, string name)
    {
        var state = await client.GetFromJsonAsync<JsonElement>("/api/triggers", TestContext.Current.CancellationToken);
        return state.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("name").GetString() == name);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the trigger.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Stands in for the API: records each trigger call and answers with <c>respond</c>, or 200.
    /// </summary>
    private sealed class FakeApi(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<Call> _calls = new();

        public IReadOnlyList<Call> Calls => [.. _calls];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _calls.Enqueue(new Call(request.Method, request.RequestUri!.ToString(), request.Content?.Headers.ContentType?.MediaType));
            return respond is null ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)) : respond(request, cancellationToken);
        }

        public sealed record Call(HttpMethod Method, string Url, string? ContentType);
    }
}
