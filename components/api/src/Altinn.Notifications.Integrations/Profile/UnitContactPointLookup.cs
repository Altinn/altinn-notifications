namespace Altinn.Notifications.Integrations.Profile
{
    /// <summary>
    /// A class describing the query model for contact points for units
    /// </summary>
    public class UnitContactPointLookup
    {
        /// <summary>
        /// Gets or sets the list of organisation numbers to lookup contact points for
        /// </summary>
        public List<string> OrganizationNumbers { get; set; } = [];

        /// <summary>
        /// Gets or sets the resource id to filter the contact points by
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;
    }
}
