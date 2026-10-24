using System.Diagnostics;
using System.Net.Http.Headers;

using Altinn.Notifications.Tools.Simulator.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.Simulator.Triggers;

/// <summary>
/// Calls the API's trigger endpoints on an interval, the way the cron jobs do in the cluster. Each job runs on its own loop.
/// </summary>
internal sealed class TriggerScheduler : BackgroundService
{
    public const string HttpClientName = "triggers";

    private const int _maxResponseLength = 2000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TriggerScheduler> _logger;
    private readonly Dictionary<string, TriggerJob> _jobs;

    public TriggerScheduler(IOptions<TriggerSettings> options, IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<TriggerScheduler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;

        var settings = options.Value;
        _jobs = new(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in TriggerCatalog.All)
        {
            var overrides = settings.Jobs.GetValueOrDefault(definition.Name) ?? new TriggerJobSettings();
            string hoursText = overrides.Hours ?? definition.Hours;
            if (!HourRange.TryParse(hoursText, out bool[] hours))
            {
                throw new InvalidOperationException($"TriggerSettings:Jobs:{definition.Name}:Hours '{hoursText}' is not a cron hour field like 7-16.");
            }

            bool enabled = settings.Enabled && (overrides.Enabled ?? definition.Enabled);
            int interval = Math.Max(overrides.IntervalSeconds ?? definition.IntervalSeconds, 1);
            _jobs[definition.Name] = new TriggerJob(definition, enabled, interval, hours, settings.HistorySize);
        }
    }

    /// <summary>
    /// Raised when a job starts or finishes a run, or its settings change, so the GUI can refresh.
    /// </summary>
    public event Action? Changed;

    public IReadOnlyList<TriggerJob> Jobs => [.. TriggerCatalog.All.Select(d => _jobs[d.Name])];

    public TriggerJob? Find(string name) => _jobs.GetValueOrDefault(name);

    public void Update(TriggerJob job, bool? enabled, int? intervalSeconds, bool[]? hours)
    {
        job.Update(enabled, intervalSeconds, hours);
        Changed?.Invoke();
    }

    /// <summary>
    /// Calls the job's trigger endpoint once.
    /// </summary>
    /// <returns>The run, or <c>null</c> when the job is already running.</returns>
    public async Task<TriggerRun?> RunAsync(TriggerJob job, string source, CancellationToken cancellationToken)
    {
        if (!await job.Running.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        try
        {
            Changed?.Invoke();
            var run = await CallAsync(job, source, cancellationToken);
            LogRun(job, run);
            job.AddRun(run);
            return run;
        }
        finally
        {
            job.Running.Release();
            Changed?.Invoke();
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(_jobs.Values.Select(job => LoopAsync(job, stoppingToken)));

    /// <summary>
    /// Waits one interval, then runs the job if it is enabled and the hour is one it runs in. A change to the job's settings ends the
    /// wait early, so a resumed job or a shorter interval takes effect at once.
    /// </summary>
    private async Task LoopAsync(TriggerJob job, CancellationToken stoppingToken)
    {
        DateTimeOffset last = _timeProvider.GetUtcNow();
        while (!stoppingToken.IsCancellationRequested)
        {
            var wake = job.WakeToken;
            var next = last.AddSeconds(job.IntervalSeconds);
            job.NextRunAt = job.Enabled ? next : null;

            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake);
            try
            {
                var delay = job.Enabled ? TimeSpan.FromTicks(Math.Max((next - _timeProvider.GetUtcNow()).Ticks, 0)) : Timeout.InfiniteTimeSpan;
                await Task.Delay(delay, _timeProvider, waiting.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // The settings changed. Keep the last run time, so a shorter interval can make the job due at once.
                continue;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            last = _timeProvider.GetUtcNow();
            if (job.RunsAt(last))
            {
                await RunAsync(job, "scheduled", stoppingToken);
            }
        }
    }

    private async Task<TriggerRun> CallAsync(TriggerJob job, string source, CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        // The controller only takes application/json, but the endpoints read no body.
        using var content = new ByteArrayContent([]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).PostAsync(TriggerCatalog.TriggerPath + job.Name, content, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TriggerRun(startedAt, stopwatch.ElapsedMilliseconds, source, (int)response.StatusCode, Truncate(body));
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            string error = ex is TaskCanceledException ? "Timed out waiting for the API." : ex.Message;
            return new TriggerRun(startedAt, stopwatch.ElapsedMilliseconds, source, null, error);
        }
    }

    /// <summary>
    /// Logs failures, and the first success after one, so a job that keeps failing while the API starts does not fill the log.
    /// </summary>
    private void LogRun(TriggerJob job, TriggerRun run)
    {
        bool failedBefore = job.History.FirstOrDefault() is { Succeeded: false };
        if (!run.Succeeded && !failedBefore)
        {
            _logger.LogWarning("Trigger {Name} failed with {Status}: {Response}", job.Name, run.Status?.ToString() ?? "no response", run.Response);
        }
        else if (run.Succeeded && failedBefore)
        {
            _logger.LogInformation("Trigger {Name} works again ({Status})", job.Name, run.Status);
        }
        else
        {
            _logger.LogDebug("Trigger {Name} -> {Status} in {Duration} ms", job.Name, run.Status, run.DurationMs);
        }
    }

    private static string Truncate(string text) => text.Length <= _maxResponseLength ? text : text[.._maxResponseLength] + "…";
}
