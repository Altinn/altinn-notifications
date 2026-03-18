namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration object used to hold integration settings for a Wolverine.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// ASB queue name used for publishing email messages.
    /// Produced by the API and consumed by the email service and Azure Communication Services.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;
}
