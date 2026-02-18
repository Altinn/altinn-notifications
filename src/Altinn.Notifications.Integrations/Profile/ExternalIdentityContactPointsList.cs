using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Response model containing a list of contact points for one or more external identity users.
/// </summary>
public record ExternalIdentityContactPointsList
{
    /// <summary>
    /// A list containing contact points for external identity users.
    /// </summary>
    public required List<ExternalIdentityContactPoints> ContactPointsList { get; init; } = [];
}
