using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Sms;

/// <summary>
/// Defines SMS configuration settings used in notification order requests.
/// </summary>
public record SmsSendingOptionsExt : SmsDetailsExt
{
    /// <summary>
    /// The policy controlling when the SMS should be delivered.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SendingTimePolicyExt.Daytime"/> to respect standard business hours (08:00-17:00 CET).
    /// </remarks>
    [JsonPropertyName("sendingTimePolicy")]
    [DefaultValue(SendingTimePolicyExt.Daytime)]
    public SendingTimePolicyExt SendingTimePolicy { get; init; } = SendingTimePolicyExt.Daytime;
}
