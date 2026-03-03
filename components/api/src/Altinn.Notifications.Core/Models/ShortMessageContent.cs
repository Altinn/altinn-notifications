namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the content and sender information for an SMS (Short Message Service) message.
/// </summary>
public record ShortMessageContent
{
    /// <summary>
    /// The sender identifier displayed in the recipient's SMS message.
    /// </summary>
    public string? Sender { get; init; }

    /// <summary>
    /// The text content of the SMS message to be delivered to the recipient.
    /// </summary>
    public required string Message { get; init; }
}
