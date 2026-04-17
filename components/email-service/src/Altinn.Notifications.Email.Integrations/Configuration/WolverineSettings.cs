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
    /// Enables or disables the Wolverine listener responsible for consuming
    /// <c>CheckEmailSendStatusCommand</c> messages from the Azure Service Bus polling‑loop queue.
    /// When <c>true</c>, the email service actively consumes these commands and polls ACS for delivery status.
    /// When <c>false</c> (default), the Kafka‑based <c>EmailSendingAcceptedConsumer</c>
    /// remains the active mechanism for processing accepted email events.
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
    /// Retry policy for the email-status-check polling-loop queue.
    /// </summary>
    public QueueRetryPolicy EmailStatusCheckQueuePolicy { get; set; } = new();
}
