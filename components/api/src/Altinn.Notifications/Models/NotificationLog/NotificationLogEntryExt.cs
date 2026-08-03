using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.NotificationLog;

/// <summary>
/// Represents a single notification log entry returned by the API.
/// </summary>
public record NotificationLogEntryExt
{
    /// <summary>
    /// Gets the identifier of the order chain this entry belongs to, or <see langword="null"/> for standalone orders.
    /// </summary>
    [JsonPropertyName("orderChainId")]
    public Guid? OrderChainId { get; init; }

    /// <summary>
    /// Gets the identifier of the shipment (notification order) that produced this log entry.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public required Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the identifier of the email or SMS notification this log entry was derived from.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// Gets the short name of the service owner that created the order.
    /// </summary>
    [JsonPropertyName("creatorName")]
    public required string CreatorName { get; init; }

    /// <summary>
    /// Gets the sender's own reference for the order, or <see langword="null"/> if none was provided.
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets the Dialogporten dialog identifier associated with this notification, or <see langword="null"/> if not applicable.
    /// </summary>
    [JsonPropertyName("dialogId")]
    public string? DialogId { get; init; }

    /// <summary>
    /// Gets the Dialogporten transmission identifier associated with this notification, or <see langword="null"/> if not applicable.
    /// </summary>
    [JsonPropertyName("transmissionId")]
    public string? TransmissionId { get; init; }

    /// <summary>
    /// Gets the provider's own tracking reference for this send attempt, or <see langword="null"/> if not yet processed.
    /// </summary>
    [JsonPropertyName("deliveryReference")]
    public string? DeliveryReference { get; init; }

    /// <summary>
    /// Gets the national identity number or organisation number of the recipient, or <see langword="null"/> when addressed directly.
    /// </summary>
    [JsonPropertyName("recipient")]
    public string? Recipient { get; init; }

    /// <summary>
    /// Gets the notification order type (e.g. <c>Notification</c>, <c>Reminder</c>, <c>Instant</c>, <c>Composed</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the notification channel (<c>Email</c> or <c>Sms</c>).
    /// </summary>
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    /// <summary>
    /// Gets the email address or mobile number the notification was sent to.
    /// </summary>
    [JsonPropertyName("destination")]
    public required string Destination { get; init; }

    /// <summary>
    /// Gets the Altinn resource identifier linked to this notification.
    /// </summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>
    /// Gets the delivery result status at the time the log entry was created.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Gets the timestamp the order requested notifications be sent at.
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public required DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// Gets the timestamp when the notification status was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    public required DateTime LastUpdateTime { get; init; }
}
