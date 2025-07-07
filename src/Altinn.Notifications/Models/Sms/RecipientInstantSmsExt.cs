using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Sms;

/// <summary>
/// Represents the model for sending an SMS to a specific mobile number.
/// </summary>
public record RecipientInstantSmsExt
{
    /// <summary>
    /// The recipient's phone number in international format.
    /// </summary>
    /// <remarks>
    /// This is the destination number where the SMS will be delivered.
    /// The phone number should include the country code (e.g., +4799999999).
    /// </remarks>
    [Required]
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// The time-to-live for the SMS message in seconds.
    /// </summary>
    /// <remarks>
    /// Specifies how long the message should be kept in the delivery system if it cannot be delivered immediately.
    /// </remarks>
    [Required]
    [JsonPropertyName("timeToLiveInSeconds")]
    public required int TimeToLiveInSeconds { get; init; }

    /// <summary>
    /// The content and sender information for the SMS message.
    /// </summary>
    /// <remarks>
    /// Contains the message body text and optional sender information that will be displayed to the recipient.
    /// </remarks>
    [Required]
    [JsonPropertyName("smsSettings")]
    public required SmsDetailsExt Details { get; init; }
}
