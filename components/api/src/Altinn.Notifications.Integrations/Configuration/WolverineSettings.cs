using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings for the Notifications API.
/// Extends the shared base with queue names consumed by the API.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Maximum number of email send commands published concurrently during a batch publish operation.
    /// </summary>
    public int EmailPublishConcurrency { get; set; } = 10;

    /// <summary>
    /// ASB queue name used for publishing email messages.
    /// Produced by the API and consumed by the email service and Azure Communication Services.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email send queue.
    /// </summary>
    public QueueRetryPolicy EmailSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name used for publishing sms messages.
    /// Produced by the API and consumed by the SMS Service and service provider.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the sms send queue.
    /// </summary>
    public QueueRetryPolicy SendSmsQueuePolicy { get; set; } = new();

    /// <summary>
    /// Whether to enable the email delivery report queue listener.
    /// </summary>
    public bool EnableEmailDeliveryReportListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving email delivery reports.
    /// Produced by the email service and Event Grid.
    /// </summary>
    public string EmailDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email delivery report queue.
    /// </summary>
    public QueueRetryPolicy EmailDeliveryReportQueuePolicy { get; set; } = new();
}
