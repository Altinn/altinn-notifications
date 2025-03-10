using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an SMS template with an associated sending time policy.
/// This class extends <see cref="SmsTemplateExt"/> by adding a policy that determines when the SMS should be sent.
/// </summary>
public class ScheduledSmsTemplateExt : SmsTemplateExt
{
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
