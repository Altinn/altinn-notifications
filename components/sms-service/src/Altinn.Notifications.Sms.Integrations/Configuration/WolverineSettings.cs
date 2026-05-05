using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names and feature flags for the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary> ASB queue name used for publishing sms messages.
    /// Produced by the API and consumed by the sms service.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS sending queue, defining the retry strategy for transient failures when processing messages from the queue. This includes parameters such as the number of retry attempts, delay between retries, and any specific exceptions that should trigger a retry.
    /// </summary>
    public QueueRetryPolicy SendSmsQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to accept sms notifications via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableSendSmsListener { get; set; } = false;
    
    /// <summary>
    /// When <c>true</c>, <c>StatusService</c> publishes SMS delivery reports to
    /// the ASB queue instead of the Kafka topic.
    /// Must match <c>WolverineSettings</c> in the SMS core project.
    /// </summary>
    public bool EnableSmsDeliveryReportPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing SMS delivery reports.
    /// Consumed by the API service's <c>SmsDeliveryReportHandler</c>.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Determines whether to publish SMS send results via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableSmsSendResultPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing SMS send results.
    /// Produced by this SMS service and consumed by the Notifications API.
    /// </summary>
    public string SmsSendResultQueueName { get; set; } = string.Empty;
}
