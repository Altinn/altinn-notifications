using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile
{
    /// <summary>
    /// A list representation of <see cref="OrganizationContactPoints"/>
    /// </summary>
    public class OrganizationContactPointsList
    {
        /// <summary>
        /// A list containing contact points for users
        /// </summary>
        public List<OrganizationContactPoints> ContactPointsList { get; set; } = [];
    }
}
