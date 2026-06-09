using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines SMS configuration settings used in notification orders.
/// </summary>
public class SmsSendingOptions
{
    /// <summary>
    /// Gets or sets the sender identifier displayed in the recipient's SMS message.
    /// </summary>
    /// <remarks>
    /// Can be either a phone number or an alphanumeric sender identifier, subject to carrier and regional restrictions.
    /// </remarks>
    public string? Sender { get; set; }

    /// <summary>
    /// Gets or sets the text content of the SMS message.
    /// </summary>
    /// <remarks>
    /// Plain text content with length constraints determined by carrier limitations and character encoding.
    /// </remarks>
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the policy controlling when the SMS should be delivered.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SendingTimePolicy.Daytime"/> to respect standard business hours (08:00-17:00 CET).
    /// </remarks>
    public SendingTimePolicy SendingTimePolicy { get; set; } = SendingTimePolicy.Daytime;
}
