using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Response model containing a list of contact points for one or more external identity users.
/// </summary>
/// <remarks>
/// External identity users include self-identified users (ID-porten email login) and username-based users.
/// </remarks>
public record ExternalIdentityContactPointsList
{
    /// <summary>
    /// A list containing contact points for external identity users.
    /// </summary>
    public required List<ExternalIdentityContactPoints> ContactPointsList { get; init; } = [];
}
