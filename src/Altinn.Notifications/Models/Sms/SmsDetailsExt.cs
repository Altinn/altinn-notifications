using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Sms;

/// <summary>
/// Represents the content and sender information for an SMS message.
/// </summary>
public record SmsDetailsExt
{
    /// <summary>
    /// The sender identifier displayed in the recipient's SMS message.
    /// </summary>
    /// <remarks>
    /// Can be either a phone number or an alphanumeric sender identifier, subject to carrier and regional restrictions.
    /// </remarks>
    [JsonPropertyName("sender")]
    public string? Sender { get; init; }

    /// <summary>
    /// The text content of the SMS message.
    /// </summary>
    /// <remarks>
    /// Plain text content with length constraints determined by carrier limitations and character encoding.
    /// </remarks>
    [Required]
    [JsonPropertyName("body")]
    public required string Body { get; init; }
}
