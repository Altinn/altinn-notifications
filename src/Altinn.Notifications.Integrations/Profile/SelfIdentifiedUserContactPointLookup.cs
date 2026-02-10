namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Represents the criteria used to look up contact details for self-identified users by their external identities.
/// </summary>
public record SelfIdentifiedUserContactPointLookup
{
    /// <summary>
    /// A list of external identities for which to retrieve contact points.
    /// </summary>
    public required List<string> ExternalIdentities { get; init; } = [];
}
