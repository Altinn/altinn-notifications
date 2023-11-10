using System.Text.Json.Serialization;

using Altinn.Notifications.Models;

namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// A class representing an email notification summary 
    /// </summary>
    /// <remarks>
    /// External representaion to be used in the API.
    /// </remarks>
    public class EmailNotificationSummaryExt
    {
        /// <summary>
        /// The order id
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// The senders reference
        /// </summary>
        public string? SendersReference { get; set; }

        /// <summary>
        /// The number of generated email notifications
        /// </summary>
        public int Generated { get; set; }

        /// <summary>
        /// The number of email notifications that were sent successfully
        /// </summary>
        public int Succeeded { get; set; }

        /// <summary>
        /// A list of notifications with send result 
        /// </summary>
        public List<EmailNotificationWithResultExt> Notifications { get; set; } = new List<EmailNotificationWithResultExt>();
    }

    /// <summary>
    /// EmailNotificationWithResultExt class
    /// </summary>
    public class EmailNotificationWithResultExt
    {
        /// <summary>
        /// The notification id
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Boolean indicating if the sending of the notification was successful
        /// </summary>
        [JsonPropertyName("succeeded")]
        public bool Succeeded { get; set; }

        /// <summary>
        /// The recipient of the notification
        /// </summary>
        [JsonPropertyName("recipient")]
        public RecipientExt Recipient { get; set; } = new();

        /// <summary>
        /// The result status of the notification
        /// </summary>
        [JsonPropertyName("sendStatus")]
        public StatusExt SendStatus { get; set; } = new();
    }
}
