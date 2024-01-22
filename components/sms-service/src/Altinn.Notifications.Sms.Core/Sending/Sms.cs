namespace Altinn.Notifications.Sms.Core.Sending
{
    /// <summary>
    /// Class representing an sms message
    /// </summary>
    public class Sms
    {
        /// <summary>
        /// Gets or sets the contents of the sms message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipient of the sms message
        /// </summary>
        public string Recipient { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender of the sms message
        /// </summary>
        /// <remarks>
        /// Can be a literal string or a phone number
        /// </remarks>
        public string Sender { get; set; } = string.Empty;
    }
}
