using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// A list representation of <see cref="UserContactPoints"/>
/// </summary>
public class UserContactPointsList
{
    /// <summary>
    /// A list containing contact points for users
    /// </summary>
    public List<UserContactPoints> ContactPointsList { get; set; } = [];
}
