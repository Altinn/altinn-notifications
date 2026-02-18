using Altinn.Urn;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Represents a URN type for external identity contact points, supporting ID-porten email and username-based users.
/// </summary>
[KeyValueUrn]
public abstract partial record ExternalIdentityUrn
{
    /// <summary>
    /// Determines whether this URN is an ID-porten email identity, and retrieves the decoded email if available.
    /// </summary>
    [UrnKey("altinn:person:idporten-email")]
    public partial bool IsIDPortenEmail(out UrnEncoded email);

    /// <summary>
    /// Determines whether this URN is a username-based identity, and retrieves the decoded username if available.
    /// </summary>
    [UrnKey("altinn:username")]
    [UrnKey("altinn:person:legacy-selfidentified")]
    [UrnKey("altinn:party:username", Canonical = true)]
    public partial bool IsUsername(out UrnEncoded username);
}
