namespace Altinn.Notifications.Integrations.Profile
{
    /// <summary>
    /// A class respresenting a user contact point lookup object
    /// </summary>
    public class UserContactPointLookup
    {
        /// <summary>
        /// A list of national identity numbers to look up contact points or contact point availability for
        /// </summary>
        public List<string> NationalIdentityNumbers { get; set; } = [];
    }
}
