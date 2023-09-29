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
    /// A pre registered list of approved email addresses that an application owner can use as from address.
    /// </summary>
    public List<string> EmailAddresses { get; set; } = new List<string>();

    /// <summary>
    /// A pre registered list of approved short names that an application owner can use as sender of an sms.
    /// </summary>
    public List<string> SmsNames { get; set; } = new List<string>();
}
