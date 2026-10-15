namespace Altinn.Notifications.Sms.Integrations.LinkMobility
{
    /// <summary>
    /// Configuration for the LinkMobility SMS gateway
    /// </summary>
    public class SmsGatewaySettings
    {
        /// <summary>
        /// Username to use for authentication towards the SMS gateway
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password to use for authentication towards the SMS gateway
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Url to the SMS gateway endpoint
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// HTTP request timeout in seconds for calls to the SMS gateway. Defaults to 30 seconds.
        /// </summary>
        public int TimeoutInSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to use a mock SMS client for local development and performance testing.
        /// </summary>
        public bool MockSmsClient { get; set; } = false;

        /// <summary>
        /// Gets or sets the base URL for the mock delivery endpoint used when MockSmsClient is enabled. This is typically used for local development and performance testing.
        /// </summary>
        public string MockDeliveryEndpointBaseUrl { get; set; } = "http://localhost:5090";
    }
}
