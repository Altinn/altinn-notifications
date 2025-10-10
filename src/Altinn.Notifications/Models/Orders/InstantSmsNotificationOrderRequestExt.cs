using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents a request to send an SMS notification immediately to a single recipient.
/// </summary>
public record InstantSmsNotificationOrderRequestExt
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
    /// The SMS recipient delivery details.
    /// </summary>
    /// <remarks>
    /// Contains the recipient's phone number, time-to-live settings, and SMS content including sender and message body.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipientSms")]
    public required ShortMessageDeliveryDetailsExt RecipientSms { get; init; }
}
