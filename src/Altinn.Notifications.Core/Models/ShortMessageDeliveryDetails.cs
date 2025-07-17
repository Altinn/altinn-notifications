namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents delivery details for an SMS including recipient, content, and delivery parameters.
/// </summary>
public record ShortMessageDeliveryDetails
{
    /// <summary>
    /// The recipient's phone number in international format.
    /// </summary>
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// The time-to-live duration, specified in seconds.
    /// </summary>
    public required int TimeToLiveInSeconds { get; init; }

    /// <summary>
    /// The content and sender information.
    /// </summary>
    public required ShortMessageContent ShortMessageContent { get; init; }
}
