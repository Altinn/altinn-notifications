using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Recipient;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents a request to send an SMS notification immediately.
/// </summary>
public class InstantNotificationOrderRequestExt
{
    /// <summary>
    /// A unique identifier used to ensure the same notification is not processed multiple times.
    /// </summary>
    /// <remarks>
    /// This value must be unique for each distinct notification attempt.
    /// If a request with the same idempotency ID is received multiple times,
    /// only the first one will be processed, and subsequent requests will return the original response.
    /// </remarks>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// A reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    /// <remarks>
    /// This value can be used for correlating the notification with the sender's systems
    /// and will be included in the response.
    /// </remarks>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets or sets the required recipient information for this reminder.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipient")]
    public required InstantNotificationRecipientExt Recipient { get; set; }
}
