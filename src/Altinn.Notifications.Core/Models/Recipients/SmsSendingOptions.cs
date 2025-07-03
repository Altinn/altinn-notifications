using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines SMS configuration settings used in notification orders.
/// </summary>
public record SmsSendingOptions : SmsDetails
{
    /// <summary>
    /// The policy controlling when the SMS should be delivered.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SendingTimePolicy.Daytime"/> to respect standard business hours (08:00-17:00 CET).
    /// </remarks>
    public SendingTimePolicy SendingTimePolicy { get; init; } = SendingTimePolicy.Daytime;
}
