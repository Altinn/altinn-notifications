namespace Altinn.Notifications.Integrations.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for all Altinn integrations.
    /// </summary>
    public class AltinnServiceSettings
    {
        /// <summary>
        /// Gets or sets the url for the API profile endpoint
        /// </summary>
        public string ApiProfileEndpoint { get; set; } = string.Empty;
    }
}
