using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an SMS template that contains all the information needed to deliver a text message to a specific mobile number.
/// </summary>
public class SmsRequestTemplateExt
{
    /// <summary>
    /// Gets or sets the phone number to which the SMS should be sent.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the SMS settings, including the payload and sending time policy.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("smsSettings")]
    public required SmsRequestSettingsExt SmsSettings { get; set; }

    /// <summary>
    /// Json serialized the <see cref="EmailNotificationOrderRequestExt"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}
