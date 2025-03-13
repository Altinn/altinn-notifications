using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the request model for sending an SMS to a specific recipient.
/// </summary>
public class RecipientSmsRequestExt
{
    /// <summary>
    /// Gets or sets the phone number to which the SMS should be delivered.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, including sender number, message content, and sending time policy.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("smsSettings")]
    public required RecipientSmsSettingsRequestExt Settings { get; set; }
}
