namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents the content and sender information for an SMS message.
/// </summary>
public record SmsDetails
{
    /// <summary>
    /// The sender identifier displayed in the recipient's SMS message.
    /// </summary>
    /// <remarks>
    /// Can be either a phone number or an alphanumeric sender identifier, subject to carrier and regional restrictions.
    /// </remarks>
    public string? Sender { get; init; }

    /// <summary>
    /// The text content of the SMS message.
    /// </summary>
    /// <remarks>
    /// Plain text content with length constraints determined by carrier limitations and character encoding.
    /// </remarks>
    public required string Body { get; init; }
}
