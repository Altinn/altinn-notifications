namespace Altinn.Notifications.Core.Models.Dashboard;

/// <summary>
/// Represents a notification order returned from a dashboard lookup, grouping all delivery attempts by channel.
/// </summary>
public record DashboardNotification
{
    /// <summary>
    /// The unique identifier for the notification order.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// The short name of the organisation that created the order.
    /// </summary>
    public string CreatorName { get; init; }

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
    public List<DashboardRecipient> Recipients { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardNotification"/> record.
    /// </summary>
    public DashboardNotification(
        Guid notificationId,
        string creatorName,
        string? resourceId,
        string? sendersReference,
        DateTime requestedSendTime,
        string? notificationChannel,
        List<DashboardRecipient> recipients)
    {
        NotificationId = notificationId;
        CreatorName = creatorName;
        ResourceId = resourceId;
        SendersReference = sendersReference;
        RequestedSendTime = requestedSendTime;
        NotificationChannel = notificationChannel;
        Recipients = recipients;
    }
}
