using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models
{
    /// <summary>
    /// Class representing a notification order request
    /// </summary>
    /// <remarks>
    /// External representation to be used in the API.
    /// </remarks>
    public class NotificationOrderRequestExt : NotificationOrderRequestBaseExt
    {
        /// <summary>
        /// Gets or sets the notification channel
        /// </summary>
        /// <remarks>
        /// Nullable to ensure that default value is never assigned as a valid notification channel
        /// </remarks>
        [JsonPropertyName("notificationChannel")]
        public NotificationChannelExt? NotificationChannel { get; set; }

        /// <summary>
        /// Gets or sets the email template
        /// </summary>
        [JsonPropertyName("emailTemplate")]
        public EmailTemplateExt? EmailTemplate { get; set; }

        /// <summary>
        /// Gets or sets the SMS template
        /// </summary>
        [JsonPropertyName("smsTemplate")]
        public SmsTemplateExt? SmsTemplate { get; set; }
    }
}
