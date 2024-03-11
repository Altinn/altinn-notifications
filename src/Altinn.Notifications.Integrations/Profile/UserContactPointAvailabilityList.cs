using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// A list representation of <see cref="UserContactPointAvailability"/>
/// </summary>
public class UserContactPointAvailabilityList
{
    /// <summary>
    /// A list containing contact point availabiliy for users
    /// </summary>
    public List<UserContactPointAvailability> AvailabilityList { get; set; } = [];
}
