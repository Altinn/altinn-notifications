using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an SMS notification template with an associated sending time policy.
/// This class extends <see cref="SmsTemplateExt"/> by adding a policy that determines when the SMS should be sent.
/// </summary>
[Description("Defines settings for SMS.")]
[JsonDerivedType(typeof(SmsTemplateWithSendingTimePolicyExt), "smsSettings")]
public class SmsTemplateWithSendingTimePolicyExt : SmsTemplateExt
{
    /// <summary>
    /// Gets or sets the sending time policy for when the SMS should be sent.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.Unrestricted;
}
