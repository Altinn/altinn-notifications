using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Models.Dashboard;

/// <summary>
/// Represents a single notification (email or SMS) returned from a dashboard lookup.
/// </summary>
public record DashboardNotification
{
    /// <summary>
    /// The unique identifier for the notification.
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
    /// The recipients of this notification.
    /// </summary>
    public List<Recipient> Recipients { get; init; }

    /// <summary>
    /// The delivery channel: "email" or "sms".
    /// </summary>
    public string Channel { get; init; }

    /// <summary>
    /// The delivery result status.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// When the result was recorded.
    /// </summary>
    public DateTime? ResultTime { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardNotification"/> record.
    /// </summary>
    /// <param name="notificationId">The unique identifier for the notification.</param>
    /// <param name="creatorName">The short name of the organisation that created the order.</param>
    /// <param name="resourceId">The Altinn resource the notification is related to.</param>
    /// <param name="sendersReference">The sender's reference for the order.</param>
    /// <param name="requestedSendTime">When the notification was requested to be sent.</param>
    /// <param name="recipients">The recipients of this notification.</param>
    /// <param name="channel">The delivery channel: "email" or "sms".</param>
    /// <param name="result">The delivery result status.</param>
    /// <param name="resultTime">When the result was recorded.</param>
    public DashboardNotification(
        Guid notificationId,
        string creatorName,
        string? resourceId,
        string? sendersReference,
        DateTime requestedSendTime,
        List<Recipient> recipients,
        string channel,
        string? result,
        DateTime? resultTime)
    {
        NotificationId = notificationId;
        CreatorName = creatorName;
        ResourceId = resourceId;
        SendersReference = sendersReference;
        RequestedSendTime = requestedSendTime;
        Recipients = recipients;
        Channel = channel;
        Result = result;
        ResultTime = resultTime;
    }
}
