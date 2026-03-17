namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration object used to hold integration settings for a Wolverine.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Whether to enable the email send queue publisher.
    /// </summary>
    public bool EnableEmailSendPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing email send messages.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;
}
