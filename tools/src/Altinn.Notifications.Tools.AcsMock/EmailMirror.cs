using System.Text.Json;
using System.Threading.Channels;

using Altinn.Notifications.Tools.AcsMock.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// Sends a copy of each accepted or rejected send and its outcome to <see cref="AcsMockSettings.MirrorUrl"/>, the test harness control
/// panel's Inbox tab. The copies are queued and posted one by one in the background, so a slow or stopped control panel never holds
/// up a send, and a copy that cannot be posted is dropped.
/// </summary>
internal sealed class EmailMirror(IOptions<AcsMockSettings> options, ILogger<EmailMirror> logger) : BackgroundService
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
    /// Queues a copy of a send as the mock answered it: accepted as <paramref name="operationId"/>, or rejected with <paramref name="error"/>.
    /// </summary>
    public void Received(string requestBody, string? operationId, EmailScenario scenario, string? error = null)
    {
        if (!IsEnabled || !TryReadEmail(requestBody, out var email))
        {
            return;
        }

        Queue("email", new
        {
            id = operationId,
            email.Sender,
            email.To,
            email.Cc,
            email.Bcc,
            email.Subject,
            email.PlainText,
            email.Html,
            operationId,
            scenario = scenario.ToString(),
            status = error is null ? null : "Failed",
            detail = error,
        });
    }

    /// <summary>
    /// Queues the outcome of an operation: a failed operation, or the status of its delivery report, e.g. <c>Bounced</c>.
    /// </summary>
    public void Completed(string operationId, string outcome)
    {
        string status = outcome == "Delivered" ? "Delivered" : "Failed";
        Queue($"email/{Uri.EscapeDataString(operationId)}/status", new { status, detail = outcome });
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

    private static bool TryReadEmail(string json, out MirrorEmail email)
    {
        email = new MirrorEmail();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var content = root.TryGetProperty("content", out var c) ? c : default;
            var recipients = root.TryGetProperty("recipients", out var r) ? r : default;
            email = new MirrorEmail
            {
                Sender = Text(root, "senderAddress"),
                To = Addresses(recipients, "to"),
                Cc = Addresses(recipients, "cc"),
                Bcc = Addresses(recipients, "bcc"),
                Subject = Text(content, "subject"),
                PlainText = Text(content, "plainText"),
                Html = Text(content, "html"),
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }

        static string? Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        static string[] Addresses(JsonElement recipients, string kind) =>
            recipients.ValueKind == JsonValueKind.Object && recipients.TryGetProperty(kind, out var list) && list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray().Select(a => Text(a, "address")).OfType<string>()]
                : [];
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
                logger.LogInformation("Email mirror to {Url} works again", _mirrorUrl);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Email mirror {Url} -> {StatusCode}", url, (int)response.StatusCode);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (!_failing)
            {
                _failing = true;
                logger.LogInformation("Email mirror to {Url} failed, copies are dropped until it answers: {Message}", _mirrorUrl, e.Message);
            }
        }
    }

    private sealed record MirrorEmail
    {
        public string? Sender { get; init; }

        public string[] To { get; init; } = [];

        public string[] Cc { get; init; } = [];

        public string[] Bcc { get; init; } = [];

        public string? Subject { get; init; }

        public string? PlainText { get; init; }

        public string? Html { get; init; }
    }
}
