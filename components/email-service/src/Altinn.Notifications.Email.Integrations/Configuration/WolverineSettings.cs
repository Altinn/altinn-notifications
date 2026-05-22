using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the email service.
/// Extends the shared base with queue names consumed by the email service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Determines whether email send commands are consumed via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableSendEmailListener { get; set; } = true;

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
    /// Determines whether to consume email status check commands via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableEmailStatusCheckListener { get; set; } = true;

    /// <summary>
    /// Determines whether email status check commands are published via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableEmailStatusCheckPublisher { get; set; } = true;

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
    /// Determines whether to publish email send results via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableEmailSendResultPublisher { get; set; } = true;

    /// <summary>
    /// ASB queue name for publishing email send results.
    /// Produced by this email service and consumed by the Notifications API.
    /// </summary>
    public string EmailSendResultQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Determines whether to publish email service rate limit notifications via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableEmailServiceRateLimitPublisher { get; set; } = true;

    /// <summary>
    /// ASB queue name for publishing email service rate limit notifications.
    /// Produced by this email service and consumed by the Notifications API.
    /// </summary>
    public string EmailServiceRateLimitQueueName { get; set; } = string.Empty;
}
