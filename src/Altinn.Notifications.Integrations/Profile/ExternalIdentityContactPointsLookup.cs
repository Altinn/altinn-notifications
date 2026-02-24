namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Represents the criteria used to look up contact details for external identity users using their external identities.
/// </summary>
/// <remarks>
/// External identity users include self-identified users (ID-porten email login) and username-based users.
/// </remarks>
public record ExternalIdentityContactPointsLookup
{
    /// <summary>
    /// A list of external identities for which to retrieve contact points.
    /// </summary>
    public required List<string> ExternalIdentities { get; init; } = [];
}
