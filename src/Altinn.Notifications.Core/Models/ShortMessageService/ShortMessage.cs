using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.ShortMessageService;

/// <summary>
/// Represents a data transfer model for sending short text messages to recipients through the Altinn Notifications SMS service.
/// </summary>
public record ShortMessage
{
    /// <summary>
    /// The text content of the message to be delivered.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The unique identifier that connects this message to its corresponding notification order.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// The recipient's phone number in the appropriate format.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// The sender identifier that appears on the recipient's device.
    /// </summary>
    public required string Sender { get; init; }

    /// <summary>
    /// The time-to-live duration in seconds.
    /// Defines the maximum period during which delivery attempts should continue before the message expires.
    /// </summary>
    public int TimeToLive { get; init; }

    /// <summary>
    /// Serializes this message to a JSON string for API communication.
    /// </summary>
    /// <returns>A JSON string representation of this message.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
