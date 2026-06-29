using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Represents a notification order returned from a dashboard lookup, grouping all delivery attempts by channel.
/// </summary>
public record DashboardNotificationExt
{
    /// <summary>
    /// The unique identifier for the notification order.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public Guid ShipmentId { get; init; }

    /// <summary>
    /// The short name of the organisation that created the order.
    /// </summary>
    [JsonPropertyName("creatorName")]
    public string CreatorName { get; init; } = string.Empty;

    /// <summary>
    /// The Altinn resource the notification is related to.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>
    /// The sender's reference for the order.
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// When the notification was requested to be sent.
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The requested notification channel from the order (e.g. "EmailPreferred", "SmsPreferred").
    /// </summary>
    [JsonPropertyName("notificationChannel")]
    public string? NotificationChannel { get; init; }

    /// <summary>
    /// The notification type from the order (e.g. "notification", "reminder", "instant").
    /// </summary>
    [JsonPropertyName("notificationType")]
    public string? NotificationType { get; init; }

    /// <summary>
    /// The delivery attempts for this notification, one per channel.
    /// </summary>
    [JsonPropertyName("deliveryAttempts")]
    public List<DashboardDeliveryAttemptExt> DeliveryAttempts { get; init; } = [];
}
