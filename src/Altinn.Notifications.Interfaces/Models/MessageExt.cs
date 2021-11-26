namespace Altinn.Notifications.Interfaces.Models
{
    /// <summary>
    /// The message to send to the target
    /// </summary>
    public class MessageExt
    {
        /// <summary>
        /// The email title for email notifications
        /// </summary>
        public string? EmailSubject    { get; set; }

        /// <summary>
        /// The email body for email notofications. HTML
        /// </summary>
        public string? EmailBody { get; set; }

        /// <summary>
        /// SMS Text message
        /// </summary>
        public string? SmsText { get; set; }

        /// <summary>
        /// Language 
        /// </summary>
        public string Langauge { get; set; } = "nb";

    }
}
