namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents application owner specific configuration.
/// </summary>
public class ApplicationOwnerConfig
{
    /// <summary>
    /// The unique identifier of an application owner.
    /// </summary>
    public string OrgId { get; set; } = string.Empty;

    /// <summary>
    /// The pre registered from address for email notifications.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;
}
