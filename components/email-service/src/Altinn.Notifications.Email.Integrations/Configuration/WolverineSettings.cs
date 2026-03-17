using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the email service.
/// Extends the shared base with queue names consumed by the email service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Whether to enable the check email send status queue listener.
    /// </summary>
    public bool EnableCheckEmailSendStatusListener { get; set; } = false;

    /// <summary>
    /// Whether to enable the check email send status queue publisher.
    /// </summary>
    public bool EnableCheckEmailSendStatusPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for check email send status operations (polling loop).
    /// </summary>
    public string CheckEmailSendStatusQueueName { get; set; } = string.Empty;
}
