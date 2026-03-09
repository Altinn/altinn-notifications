using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings for the Notifications API.
/// Extends the shared base with queue names consumed by the API.
/// </summary>
public class WolverineSettings : Shared.Configuration.WolverineSettings
{
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
    /// ASB queue name for receiving SMS delivery reports.
    /// Produced by the SMS service delivery report controller.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS delivery report queue.
    /// </summary>
    public QueueRetryPolicy SmsDeliveryReportQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for receiving email status updates.
    /// Produced by the email service after the ACS polling loop completes.
    /// </summary>
    public string EmailStatusUpdatedQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email status updated queue.
    /// </summary>
    public QueueRetryPolicy EmailStatusUpdatedQueuePolicy { get; set; } = new();
}
