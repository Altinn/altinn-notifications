namespace Altinn.Notifications.Integrations.Configuration
{
    /// <summary>
    /// Configuration for the SMTP client
    /// </summary>
    public class SmtpSettings
    {
        /// <summary>
        /// Gets or sets the SMTP host
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the SMTP port
        /// </summary>
        public int Port { get; set; } = 465;

        /// <summary>
        /// Sender address
        /// </summary>
        public string Sender { get; set; }
    }
}