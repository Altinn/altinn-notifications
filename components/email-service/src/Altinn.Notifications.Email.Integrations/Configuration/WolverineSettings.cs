using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the email service.
/// Extends the shared base with queue names consumed by the email service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
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
    /// Number of concurrent listeners for the email send queue per pod.
    /// </summary>
    public int EmailSendListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for receiving composed email send commands.
    /// Produced by the API and consumed by this email service on a dedicated listener.
    /// </summary>
    public string ComposedEmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the composed email send queue.
    /// </summary>
    public QueueRetryPolicy ComposedEmailSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the composed email send queue per pod.
    /// </summary>
    public int ComposedEmailSendListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for email status check operations (polling loop).
    /// </summary>
    public string EmailStatusCheckQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email status check queue.
    /// </summary>
    public QueueRetryPolicy EmailStatusCheckQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the email status check queue per pod.
    /// </summary>
    public int EmailStatusCheckListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for publishing email send results.
    /// Produced by this email service and consumed by the Notifications API.
    /// </summary>
    public string EmailSendResultQueueName { get; set; } = string.Empty;

    /// <summary>
    /// ASB queue name for publishing email service rate limit notifications.
    /// Produced by this email service and consumed by the Notifications API.
    /// </summary>
    public string EmailServiceRateLimitQueueName { get; set; } = string.Empty;
}
