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
    }
}
