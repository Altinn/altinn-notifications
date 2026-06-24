namespace Altinn.Notifications.Core.Models.Dashboard;

/// <summary>
/// Represents a notification order returned from a dashboard lookup, grouping all delivery attempts by channel.
/// </summary>
public record DashboardNotification
{
    /// <summary>
    /// The unique identifier for the notification order.
    /// </summary>
    public Guid ShipmentId { get; init; }

    /// <summary>
    /// The short name of the organisation that created the order.
    /// </summary>
    public string CreatorName { get; init; }

    /// <summary>
    /// Th type of notification for the order (e.g. "notification", "reminder", "instant")
    /// </summary>
    public string NotificationType { get; init; }

    /// <summary>
    /// The Altinn resource the notification is related to.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// The sender's reference for the order.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// When the notification was requested to be sent.
    /// </summary>
    public DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The requested notification channel from the order (e.g. "EmailPreferred", "SmsPreferred").
    /// </summary>
    public string? NotificationChannel { get; init; }

    /// <summary>
    /// The delivery attempts for this notification, one per channel.
    /// </summary>
    public List<DashboardDeliveryAttempt> DeliveryAttempts { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardNotification"/> record.
    /// </summary>
    public DashboardNotification(
        Guid shipmentId,
        string creatorName,
        string? resourceId,
        string? sendersReference,
        DateTime requestedSendTime,
        string? notificationChannel,
        string notificationType,
        List<DashboardDeliveryAttempt> deliveryAttempts)
    {
        ShipmentId = shipmentId;
        CreatorName = creatorName;
        ResourceId = resourceId;
        SendersReference = sendersReference;
        RequestedSendTime = requestedSendTime;
        NotificationChannel = notificationChannel;
        NotificationType = notificationType;
        DeliveryAttempts = deliveryAttempts;
    }
}
