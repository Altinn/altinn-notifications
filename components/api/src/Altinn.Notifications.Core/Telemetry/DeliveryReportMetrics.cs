using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Altinn.Notifications.Core.Telemetry;

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
    /// <param name="messageId">The ACS message ID.</param>
    /// <param name="internetMessageId">The internet message ID (SMTP Message-ID header).</param>
    /// <param name="status">The delivery status string (e.g. "Delivered", "Failed").</param>
    /// <param name="statusMessage">The human-readable status detail message.</param>
    /// <param name="recipientMailServerHostName">The recipient mail server hostname from the delivery status details.</param>
    /// <param name="sender">The sender email address (will be masked before recording).</param>
    /// <param name="recipient">The recipient email address (will be masked before recording).</param>
    public void RecordEmailDeliveryReport(
        string? messageId,
        string? internetMessageId,
        string? status,
        string? statusMessage,
        string? recipientMailServerHostName,
        string? sender,
        string? recipient)
    {
        var tags = new TagList
        {
            { "channel",                          "email" },
            { "email.message_id",                 messageId ?? string.Empty },
            { "email.internet_message_id",        internetMessageId ?? string.Empty },
            { "email.status",                     status ?? string.Empty },
            { "email.status_message",             statusMessage ?? string.Empty },
            { "email.recipient_mail_server",      recipientMailServerHostName ?? string.Empty },
            { "email.sender",                     MaskEmailAddress(sender) },
            { "email.recipient",                  MaskEmailAddress(recipient) },
        };

        _deliveryReportStatusCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a delivery report outcome for an SMS notification received from Link Mobility via ASB.
    /// </summary>
    /// <param name="gatewayReference">The gateway reference from the SMS provider.</param>
    /// <param name="sendResult">The string representation of the send result.</param>
    /// <param name="notificationId">The optional internal notification ID.</param>
    public void RecordSmsDeliveryReport(
        string gatewayReference,
        string sendResult,
        string? notificationId)
    {
        var tags = new TagList
        {
            { "channel",                   "sms" },
            { "sms.gateway_reference",     gatewayReference },
            { "sms.send_result",           sendResult },
            { "sms.notification_id",       notificationId ?? string.Empty },
        };

        _deliveryReportStatusCounter.Add(1, tags);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }

    /// <summary>
    /// Masks an email address by keeping only the first two characters of the local part
    /// and the full domain, replacing the remainder with asterisks.
    /// Returns an empty string if the input is null or whitespace.
    /// </summary>
    internal static string MaskEmailAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return email; // not a recognisable email format — return as-is
        }

        var localPart = email[..atIndex];
        var domain = email[(atIndex + 1)..];

        if (localPart.Length <= 2)
        {
            return $"***@{domain}";
        }

        return $"{localPart[..2]}***@{domain}";
    }
}
