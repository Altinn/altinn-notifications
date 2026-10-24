using System.Net.Http.Headers;
using System.Text;
using System.Threading.Channels;

using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// A delivery report waiting to be sent for an accepted message.
/// </summary>
internal sealed record PendingDeliveryReport(string Reference, string Recipient, SmsScenario Scenario, string? TraceParent, string? TraceState);

/// <summary>
/// Posts a delivery report to the SMS service.
/// </summary>
internal interface IDeliveryReportSender
{
    /// <summary>
    /// Posts the report and returns the HTTP status code the SMS service answered with.
    /// </summary>
    Task<int> SendAsync(string report, string? traceParent, string? traceState, CancellationToken cancellationToken);
}

/// <summary>
/// Posts the report with basic auth, as LinkMobility does to the endpoint registered for the account.
/// </summary>
internal sealed class HttpDeliveryReportSender(IOptions<LinkMobilityProxySettings> options) : IDeliveryReportSender, IDisposable
{
    private readonly LinkMobilityProxySettings _settings = options.Value;
    private readonly HttpClient _client = new();

    public async Task<int> SendAsync(string report, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.DeliveryReportUrl)
        {
            Content = new StringContent(report, Encoding.UTF8, "application/xml"),
        };

        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.DeliveryReportUsername}:{_settings.DeliveryReportPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Continue the send's trace, so the report shows up under the same trace in the dashboard.
        if (!string.IsNullOrEmpty(traceParent))
        {
            request.Headers.TryAddWithoutValidation("traceparent", traceParent);
        }

        if (!string.IsNullOrEmpty(traceState))
        {
            request.Headers.TryAddWithoutValidation("tracestate", traceState);
        }

        using var response = await _client.SendAsync(request, cancellationToken);
        return (int)response.StatusCode;
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>
/// Sends a delivery report some time after a message was accepted, like LinkMobility does once the operator has answered.
/// </summary>
internal sealed class DeliveryReportService(
    IDeliveryReportSender sender,
    SmsMirror mirror,
    IOptions<LinkMobilityProxySettings> options,
    TimeProvider timeProvider,
    ILogger<DeliveryReportService> logger) : BackgroundService
{
    // The SMS service may still be starting when the first report is due.
    private const int Attempts = 3;

    private readonly Channel<PendingDeliveryReport> _queue = Channel.CreateUnbounded<PendingDeliveryReport>();
    private readonly LinkMobilityProxySettings _settings = options.Value;

    internal bool IsEnabled => !string.IsNullOrWhiteSpace(_settings.DeliveryReportUrl);

    /// <summary>
    /// Queues the report of an accepted message, unless its scenario has none.
    /// </summary>
    public void Queue(PendingDeliveryReport report)
    {
        if (IsEnabled && SmsScenarios.DeliveryState(report.Scenario) is not null)
        {
            _queue.Writer.TryWrite(report);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("LinkMobility proxy sends no delivery reports, since DeliveryReportUrl is empty");
            return;
        }

        await foreach (var report in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _ = SendAsync(report, stoppingToken);
        }
    }

    private async Task SendAsync(PendingDeliveryReport pending, CancellationToken cancellationToken)
    {
        string state = SmsScenarios.DeliveryState(pending.Scenario)!;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.DeliveryReportDelaySeconds), timeProvider, cancellationToken);
            string report = GatewayXml.DeliveryReport(pending.Reference, pending.Recipient, state, timeProvider.GetLocalNow());
            mirror.Delivered(pending.Reference, state);

            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                int statusCode = await TrySendAsync(report, pending, cancellationToken);
                logger.Log(
                    statusCode is >= 200 and < 300 ? LogLevel.Information : LogLevel.Warning,
                    "LinkMobility delivery report {State} ({Scenario}) for {Reference} to {Url} -> {StatusCode}, attempt {Attempt}\nReport: {Report}",
                    state,
                    pending.Scenario,
                    pending.Reference,
                    _settings.DeliveryReportUrl,
                    statusCode,
                    attempt,
                    PayloadMasker.Mask(report));

                if (statusCode is >= 200 and < 300)
                {
                    return;
                }

                if (attempt < Attempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "LinkMobility proxy failed to send the delivery report for {Reference}", pending.Reference);
        }
    }

    // 0 when the SMS service could not be reached, so it is retried like an error status.
    private async Task<int> TrySendAsync(string report, PendingDeliveryReport pending, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.SendAsync(report, pending.TraceParent, pending.TraceState, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return 0;
        }
    }
}
