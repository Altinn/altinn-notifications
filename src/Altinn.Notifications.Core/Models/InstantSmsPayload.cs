using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents an internal payload for sending an SMS instantly via the Notifications SMS service.
/// </summary>
public record InstantSmsPayload
{
    /// <summary>
    /// The identifier or name displayed as the sender on the recipient’s device.
    /// </summary>
    public required string Sender { get; init; }

    /// <summary>
    /// The text content of the SMS message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The recipient’s phone number.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// Time-to-live for SMS delivery attempts, in seconds.
    /// </summary>
    public int TimeToLive { get; init; }

    /// <summary>
    /// The unique identifier linking this SMS to the notification record.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Serializes the <see cref="Sms"/> object to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the <see cref="Sms"/> object.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
