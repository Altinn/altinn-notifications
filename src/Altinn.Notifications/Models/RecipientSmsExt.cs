using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an SMS template that contains all the information needed to deliver a text message to a specific mobile number.
/// </summary>
public class RecipientSmsExt
{
    /// <summary>
    /// Gets or sets the phone number to which the SMS should be sent.
    /// </summary>
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the SMS settings, including the payload and sending time policy.
    /// </summary>
    [JsonPropertyName("smsSettings")]
    public ScheduledSmsTemplateExt SmsSettings { get; set; } = new ScheduledSmsTemplateExt();
}
