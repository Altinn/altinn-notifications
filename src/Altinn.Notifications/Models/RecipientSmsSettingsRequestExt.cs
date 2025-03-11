using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents configuration settings that are associated with the request model for sending an SMS to a specific recipient.
/// </summary>
public class RecipientSmsSettingsRequestExt
{
    /// <summary>
    /// Gets or sets the phone number used as the sender in the SMS message.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("senderNumber")]
    public required string SenderNumber { get; set; }

    /// <summary>
    /// Gets or sets the text body of the SMS message.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the sending time policy, indicating when the SMS should be dispatched.
    /// Defaults to <see cref="SendingTimePolicyExt.WorkingDaysDaytime"/>.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.WorkingDaysDaytime;
}
