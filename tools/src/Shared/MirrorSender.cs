using System.Net.Http.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Tools.Shared;

/// <summary>
/// Sends copies of what a mock received to the test harness control panel. The copies are queued and posted one by one in the
/// background, so a slow or stopped control panel never holds up a send, and a copy that cannot be posted is dropped.
/// Linked into each mock that mirrors to the control panel, since the tools have no shared library.
/// </summary>
/// <param name="mirrorUrl">The control panel's mirror endpoint, e.g. <c>http://localhost:5096/mirror/</c>. Empty turns mirroring off.</param>
/// <param name="name">What is mirrored, for the log, e.g. <c>SMS</c>.</param>
/// <param name="logger">The mock's logger.</param>
internal abstract class MirrorSender(string mirrorUrl, string name, ILogger logger) : BackgroundService
{
    // Enough for a burst from a load test. When the control panel cannot keep up, newer copies are dropped.
    private const int QueueSize = 1000;

    private readonly Channel<(string Path, object Body)> _queue = Channel.CreateBounded<(string, object)>(
        new BoundedChannelOptions(QueueSize) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly string _mirrorUrl = mirrorUrl.Trim();
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(2) };
    private bool _failing;

    internal bool IsEnabled => _mirrorUrl.Length > 0;

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Queues <paramref name="body"/> to be posted as JSON to <paramref name="path"/> under the mirror URL. Does nothing when mirroring is off.
    /// </summary>
    protected void Queue(string path, object body)
    {
        if (IsEnabled)
        {
            _queue.Writer.TryWrite((path, body));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        string baseUrl = $"{_mirrorUrl.TrimEnd('/')}/";
        await foreach (var (path, body) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await PostAsync(baseUrl + path, body, stoppingToken);
        }
    }

    // Logs once when the control panel stops answering and once when it answers again, not for every copy.
    // The failure is logged after the catch, with the message only: a stack trace says nothing about a control panel that is not running.
    private async Task PostAsync(string url, object body, CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            using var response = await _client.PostAsJsonAsync(url, body, cancellationToken);
            if (_failing)
            {
                _failing = false;
                logger.LogInformation("{Name} mirror to {Url} works again", name, _mirrorUrl);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("{Name} mirror {Url} -> {StatusCode}", name, url, (int)response.StatusCode);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            error = e.Message;
        }

        if (error is not null && !_failing)
        {
            _failing = true;
            logger.LogInformation("{Name} mirror to {Url} failed, copies are dropped until it answers: {Message}", name, _mirrorUrl, error);
        }
    }
}
