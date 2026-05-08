using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Altinn.Notifications.Integrations.Telemetry;

/// <summary>
/// Provides custom OpenTelemetry metrics for ACS and SMS delivery reports.
/// Emits a <c>delivery_report_status</c> counter with relevant dimensions that map
/// to the previous <c>customDimensions</c> schema used in HTTP request logs,
/// ensuring continuity for Grafana dashboards via Azure Monitor <c>customMetrics</c>.
/// </summary>
public sealed class DeliveryReportMetrics : IDisposable
{
    /// <summary>
    /// The name of the OpenTelemetry meter. Must be registered via <c>AddMeter</c>
    /// in <c>Program.cs</c> so measurements are exported to Azure Monitor.
    /// </summary>
    public const string MeterName = "Altinn.Notifications.DeliveryReport";

    /// <summary>
    /// The name of the counter metric emitted for each processed delivery report.
    /// </summary>
    public const string DeliveryReportStatusCounterName = "delivery_report_status";

    private readonly Meter _meter;
    private readonly Counter<long> _deliveryReportStatusCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryReportMetrics"/> class.
    /// </summary>
    public DeliveryReportMetrics()
    {
        _meter = new Meter(MeterName);
        _deliveryReportStatusCounter = _meter.CreateCounter<long>(
            DeliveryReportStatusCounterName,
            unit: "{reports}",
            description: "Counts delivery report outcomes from ACS (email) and Link Mobility (SMS).");
    }

    /// <summary>
    /// Records a delivery report outcome for an email notification received from ACS.
    /// </summary>
    /// <param name="status">The delivery status string (e.g. "Delivered", "Failed").</param>
    /// <param name="statusMessage">The human-readable status detail message.</param>
    /// <param name="recipientMailServerHostName">The recipient mail server hostname from the delivery status details.</param>
    /// <param name="sender">The sender email address (will be masked before recording).</param>
    /// <param name="recipient">The recipient email address (will be masked before recording).</param>
    public void RecordEmailDeliveryReport(
        string? status,
        string? statusMessage,
        string? recipientMailServerHostName,
        string? sender,
        string? recipient)
    {
        var tags = new TagList
        {
            { "channel",                          "email" },
            { "email.status",                     status ?? string.Empty },
            { "email.status_message",             EmailMaskingHelper.RedactEmailAddressesFromMessage(statusMessage, recipient ?? string.Empty, sender ?? string.Empty) },
            { "email.recipient_mail_server",      recipientMailServerHostName ?? string.Empty },
            { "email.sender",                     EmailMaskingHelper.MaskEmailAddress(sender) },
            { "email.recipient",                  EmailMaskingHelper.MaskEmailAddress(recipient) },
        };

        _deliveryReportStatusCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a delivery report outcome for an SMS notification received from Link Mobility via ASB.
    /// </summary>
    /// <param name="sendResult">The string representation of the send result.</param>
    public void RecordSmsDeliveryReport(
        string? sendResult)
    {
        var tags = new TagList
        {
            { "channel",          "sms" },
            { "sms.send_result",  sendResult ?? string.Empty },
        };

        _deliveryReportStatusCounter.Add(1, tags);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
