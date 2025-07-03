namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents the content and sender information for an SMS message.
/// </summary>
public record SmsDetails
{
    /// <summary>
    /// The sender identifier displayed to the recipient.
    /// </summary>
    /// <remarks>
    /// Can be a phone number or an alphanumeric sender ID, subject to carrier and regional restrictions.
    /// </remarks>
    public string? Sender { get; init; }

    /// <summary>
    /// The SMS message content.
    /// </summary>
    /// <remarks>
    /// Plain text content. Message length and encoding may affect how it is delivered and billed by the carrier.
    /// </remarks>
    public required string Body { get; init; }
}
