using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models
{
    /// <summary>
    /// Represents an SMS that should be sent to a specific recipient.
    /// </summary>
    [Description("Defines settings for SMS.")]
    public class RecipientSmsExt
    {
        /// <summary>
        /// Gets or sets the phone number to which the SMS should be sent.
        /// </summary>
        [JsonPropertyName("phoneNumber")]
        public required string PhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets the SMS settings.
        /// </summary>
        /// <value>
        /// The SMS settings.
        /// </value>
        [Required]
        [JsonPropertyName("smsSettings")]
        public SmsTemplateWithSendingTimePolicyExt? SmsSettings { get; set; }
    }
}
