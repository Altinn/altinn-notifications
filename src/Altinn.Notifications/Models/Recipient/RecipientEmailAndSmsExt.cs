using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Defines a request for sending both email and SMS notifications to specific addresses.
/// </summary>
/// <remarks>
/// This class is used in the API for configuring notification delivery to a single recipient
/// through both email and SMS channels simultaneously, with specific content and delivery preferences for each channel.
/// </remarks>
public class RecipientEmailAndSmsExt
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    /// <remarks>
    /// This is the destination address where the email will be delivered.
    /// </remarks>
    [Required]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the recipient's phone number in international format.
    /// </summary>
    /// <remarks>
    /// This is the destination number where the SMS will be delivered.
    /// The phone number should include the country code (e.g., +4799999999).
    /// </remarks>
    [Required]
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the email message.
    /// </summary>
    /// <remarks>
    /// These settings control how and when the email will be composed and delivered to the recipient.
    /// </remarks>
    [Required]
    [JsonPropertyName("emailSettings")]
    public required EmailSendingOptionsExt EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the SMS message.
    /// </summary>
    /// <remarks>
    /// Contains sender information, message content, and delivery timing preferences.
    /// </remarks>
    [Required]
    [JsonPropertyName("smsSettings")]
    public required SmsSendingOptionsExt SmsSettings { get; set; }
}
