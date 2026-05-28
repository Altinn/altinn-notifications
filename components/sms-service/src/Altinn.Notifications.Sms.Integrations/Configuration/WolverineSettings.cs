using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names specific to the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// ASB queue name used for publishing SMS messages.
    /// Produced by the API and consumed by the SMS service.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS send queue.
    /// </summary>
    public QueueRetryPolicy SendSmsQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the SMS send queue per Pod.
    /// Defaults to 10 if not configured.
    /// </summary>
    public int SendSmsListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for publishing SMS delivery reports.
    /// Consumed by the API service's <c>SmsDeliveryReportHandler</c>.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// ASB queue name for publishing SMS send results.
    /// Produced by this SMS service and consumed by the Notifications API.
    /// </summary>
    public string SmsSendResultQueueName { get; set; } = string.Empty;
}
