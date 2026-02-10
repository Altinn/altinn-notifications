using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Response model containing a list of contact points for one or more self-identified user.
/// </summary>
public record SelfIdentifiedUserContactPointsList
{
    /// <summary>
    /// A list containing contact points for self-identified users.
    /// </summary>
    public required List<SelfIdentifiedUserContactPoints> ContactPointsList { get; init; } = [];
}
