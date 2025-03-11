using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an SMS template with an associated sending time policy.
/// </summary>
public class SmsRequestSettingsExt
{
    /// <summary>
    /// Gets the number from which the SMS is created by the template    
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("senderNumber")]
    public required string SenderNumber { get; set; }

    /// <summary>
    /// Gets the body of SMSs created by the template    
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the sending time policy for when the SMS should be sent.
    /// </summary>
    /// <value>
    /// The sending time policy, which determines the schedule for sending the SMS.
    /// </value>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.WorkingDaysDaytime;
}
