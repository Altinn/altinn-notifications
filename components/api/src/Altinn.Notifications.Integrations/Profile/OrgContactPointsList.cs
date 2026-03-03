using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile
{
    /// <summary>
    /// A list representation of <see cref="OrganizationContactPoints"/>
    /// </summary>
    public class OrgContactPointsList
    {
        /// <summary>
        /// A list containing contact points for organizations
        /// </summary>
        public List<OrganizationContactPoints> ContactPointsList { get; set; } = [];
    }
}
