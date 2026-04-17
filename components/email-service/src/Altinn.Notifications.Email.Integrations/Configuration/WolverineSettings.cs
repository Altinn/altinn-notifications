using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the email service.
/// Extends the shared base with queue names consumed by the email service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Determines whether to accept Email notifications via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableSendEmailListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving email send commands.
    /// Produced by the API and consumed by this email service.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email send queue.
    /// </summary>
    public QueueRetryPolicy EmailSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to consume email status check commands via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableEmailStatusCheckListener { get; set; } = false;

    /// <summary>
    /// Enables or disables the publisher responsible for sending <c>CheckEmailSendStatusCommand</c> messages independently of the listener activation, allowing
    /// the listener to be enabled without the publisher and vice versa. This is useful for testing and allows for flexibility in how the polling loop is triggered.
    /// </summary>
    public bool EnableEmailStatusCheckPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for email status check operations (polling loop).
    /// </summary>
    public string EmailStatusCheckQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email status check queue.
    /// </summary>
    public QueueRetryPolicy EmailStatusCheckQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to publish email send results via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableEmailSendResultPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing email send results.
    /// Produced by this email service and consumed by the Notifications API.
    /// </summary>
    public string EmailSendResultQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email send result queue.
    /// </summary>
    public QueueRetryPolicy EmailSendResultQueuePolicy { get; set; } = new();
}
