using System.Net.Http.Json;
using System.Threading.Channels;

using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Sends a copy of each send and its delivery state to <see cref="LinkMobilityProxySettings.MirrorUrl"/>, the test harness control
/// panel's Phone tab. The copies are queued and posted one by one in the background, so a slow or stopped control panel never holds
/// up a send, and a copy that cannot be posted is dropped.
/// </summary>
internal sealed class SmsMirror(IOptions<LinkMobilityProxySettings> options, ILogger<SmsMirror> logger) : BackgroundService
{
    // Enough for a burst from a load test. When the control panel cannot keep up, newer copies are dropped.
    private const int QueueSize = 1000;

    private readonly Channel<(string Path, object Body)> _queue = Channel.CreateBounded<(string, object)>(
        new BoundedChannelOptions(QueueSize) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly string _mirrorUrl = options.Value.MirrorUrl.Trim();
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(2) };
    private bool _failing;

    internal bool IsEnabled => _mirrorUrl.Length > 0;

    /// <summary>
    /// Queues a copy of a message as the stub answered it: accepted with a reference, or rejected with an error.
    /// </summary>
    public void Sent(SmsMessage message, SmsResult result, SmsScenario scenario)
    {
        Queue("sms", new
        {
            id = result.Reference,
            sender = message.Sender,
            recipient = message.Recipient,
            text = message.Text,
            ttl = message.Ttl,
            reference = result.Reference,
            scenario = scenario.ToString(),
            status = result.Reference is null ? "Failed" : null,
            detail = result.Error,
        });
    }

    /// <summary>
    /// Queues the state of a delivery report, e.g. <c>DELIVRD</c>, for the message with this gateway reference.
    /// </summary>
    public void Delivered(string reference, string state)
    {
        string status = state switch
        {
            "DELIVRD" => "Delivered",
            "EXPIRED" => "Expired",
            _ => "Failed",
        };

        Queue($"sms/{Uri.EscapeDataString(reference)}/status", new { status, detail = state });
    }

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        string baseUrl = _mirrorUrl.TrimEnd('/') + "/";
        await foreach (var (path, body) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await PostAsync(baseUrl + path, body, stoppingToken);
        }
    }

    private void Queue(string path, object body)
    {
        if (IsEnabled)
        {
            _queue.Writer.TryWrite((path, body));
        }
    }

    // Logs once when the control panel stops answering and once when it answers again, not for every copy.
    private async Task PostAsync(string url, object body, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.PostAsJsonAsync(url, body, cancellationToken);
            if (_failing)
            {
                _failing = false;
                logger.LogInformation("SMS mirror to {Url} works again", _mirrorUrl);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("SMS mirror {Url} -> {StatusCode}", url, (int)response.StatusCode);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (!_failing)
            {
                _failing = true;
                logger.LogInformation("SMS mirror to {Url} failed, copies are dropped until it answers: {Message}", _mirrorUrl, e.Message);
            }
        }
    }
}
