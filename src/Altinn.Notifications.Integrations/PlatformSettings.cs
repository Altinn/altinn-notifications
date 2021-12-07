namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// Represents settings needed for communication with the Profile components.
    /// </summary>
    public class PlatformSettings
    {
        /// <summary>
        /// Gets or sets the Profile component API base url.
        /// </summary>
        public string ProfileEndpointAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the subscription key request header.
        /// </summary>
        public string ProfileSubscriptionKeyHeaderName { get; set; } = "Ocp-Apim-Subscription-Key";

        /// <summary>
        /// Gets or sets the API Management subscription key to use in calls to Profile.
        /// </summary>
        public string ProfileSubscriptionKey { get; set; } = string.Empty;
    }
}
