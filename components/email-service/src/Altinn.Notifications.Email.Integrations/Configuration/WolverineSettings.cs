using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the email service.
/// Extends the shared base with queue names consumed by the email service.
/// </summary>
public class WolverineSettings : Shared.Configuration.WolverineSettings
{
    /// <summary>
    /// ASB queue name for receiving email send requests.
    /// Produced by the API when a new email send request is received.
    /// </summary>
    public string SendEmailQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the send email queue.
    /// </summary>
    public QueueRetryPolicy SendEmailQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for receiving email sending accepted messages.
    /// Produced by the email service itself after ACS accepts a send request.
    /// </summary>
    public string EmailSendingAcceptedQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email sending accepted queue.
    /// </summary>
    public QueueRetryPolicy EmailSendingAcceptedQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for publishing email status updates to the API.
    /// Produced when the ACS polling loop completes with a terminal status.
    /// Equivalent to the Kafka <c>altinn.notifications.email.status.updated</c> topic.
    /// </summary>
    public string EmailStatusUpdatedQueueName { get; set; } = string.Empty;
}
