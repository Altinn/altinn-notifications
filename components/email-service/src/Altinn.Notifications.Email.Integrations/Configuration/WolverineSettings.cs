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
    /// Determines whether to accept Email notifications via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableSendEmailListener { get; set; } = false;
    /// <summary>
    /// Determines whether the check-send-status should happen through Wolverine/Azure Service Bus.
    /// </summary>
    public bool EnableCheckEmailSendStatus { get; set; } = false;

    /// <summary>
    /// ASB queue name for check email send status operations (polling loop).
    /// </summary>
    public string CheckEmailSendStatusQueueName { get; set; } = string.Empty;
}
