using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names consumed by the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary> ASB queue name used for publishing sms messages.
    /// Produced by the API and consumed by the sms service.
    /// </summary>
    public string SmsSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS sending queue, defining the retry strategy for transient failures when processing messages from the queue. This includes parameters such as the number of retry attempts, delay between retries, and any specific exceptions that should trigger a retry.
    /// </summary>
    public QueueRetryPolicy SmsSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to accept sms notifications via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool AcceptSmsNotificationsViaWolverine { get; set; } = false;
}
