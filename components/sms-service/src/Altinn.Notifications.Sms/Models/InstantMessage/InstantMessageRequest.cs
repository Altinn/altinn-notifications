using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Sms.Models.InstantMessage;

/// <summary>
/// Represents a request model for sending a short message to a single recipient instantly.
/// </summary>
public record InstantMessageRequest
{
    /// <summary>
    /// The message content to be delivered.
    /// </summary>
    [Required]
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// The unique identifier of the notification order, of which this message is a part.
    /// </summary>
    [Required]
    [JsonPropertyName("notificationId")]
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// The recipient's phone number where the message will be sent.
    /// </summary>
    [Required]
    [JsonPropertyName("recipient")]
    public required string Recipient { get; init; }

    /// <summary>
    /// The sender of this message, typically the service name, phone number, or identifier.
    /// </summary>
    [Required]
    [JsonPropertyName("sender")]
    public required string Sender { get; init; }

    /// <summary>
    /// The time to live in seconds after which the message is expired.
    /// </summary>
    [Required]
    [JsonPropertyName("timeToLive")]
    [Range(1, 172800, ErrorMessage = "TimeToLive must be between 1 second and 48 hours")]
    public required int TimeToLive { get; init; }
}
