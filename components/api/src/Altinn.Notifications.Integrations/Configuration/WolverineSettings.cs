using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings for the Notifications API.
/// Extends the shared base with queue names consumed by the API.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
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

    /// <summary>
    /// Whether to enable the SMS delivery report queue listener.
    /// </summary>
    public bool EnableSmsDeliveryReportListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving SMS delivery reports.
    /// Published by the SMS service when <c>EnableSmsDeliveryReportPublisher</c> is <c>true</c> there.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS delivery report queue.
    /// </summary>
    public QueueRetryPolicy SmsDeliveryReportQueuePolicy { get; set; } = new();
}
