namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents application owner specific configuration.
/// </summary>
public class ApplicationOwnerConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationOwnerConfig"/> class for the given org id.
    /// </summary>
    /// <param name="orgId">The unique org id for an application owner.</param>
    public ApplicationOwnerConfig(string orgId)
    {
        OrgId = orgId;
    }

    /// <summary>
    /// The unique identifier of an application owner.
    /// </summary>
    public string OrgId { get; }

    /// <summary>
    /// A pre registered list of approved email addresses that an application owner can use as from address.
    /// </summary>
    public List<string> EmailAddresses { get; set; } = new List<string>();

    /// <summary>
    /// A pre registered list of approved short names that an application owner can use as sender of an sms.
    /// </summary>
    public List<string> SmsNames { get; set; } = new List<string>();
}
