using System.Text.Json.Serialization;

namespace Altinn.Notifications.Integrations.Profile
{
    /// <summary>
    /// A class describing the query model for contact points for organizations
    /// </summary>
    public class OrgContactPointLookup
    {
        /// <summary>
        /// Gets or sets the list of organization numbers to lookup contact points for
        /// </summary>
        [JsonPropertyName("organizationNumbers")]
        public List<string> OrganizationNumbers { get; set; } = [];
    }
}
