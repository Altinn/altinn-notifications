using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Recipient;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents a request to send a notification immediately to a single recipient.
/// </summary>
public record InstantNotificationOrderRequestExt
{
    /// <summary>
    /// The unique identifier used to ensure the same notification is not processed multiple times.
    /// </summary>
    /// <remarks>
    /// This value must be unique for each distinct notification order.
    /// If a request with the same idempotency identifier is received multiple times,
    /// only the first one will be processed, and subsequent requests will return the original response.
    /// </remarks>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    /// <remarks>
    /// This optional value can be used for correlating the notification with the sender's systems
    /// and will be included in the notification response.
    /// </remarks>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// The recipient information for this notification.
    /// </summary>
    /// <remarks>
    /// Contains all necessary information about the recipient, including contact details
    /// and delivery settings needed to send the notification.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipient")]
    public required InstantNotificationRecipientExt InstantNotificationRecipient { get; init; }
}
