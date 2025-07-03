using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Recipients;

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
    /// The SMS recipient information and message content.
    /// </summary>
    /// <remarks>
    /// Contains the destination phone number, message content,
    /// time-to-live setting, and sender information.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipientSms")]
    public required RecipientInstantSms RecipientSms { get; init; }
}
