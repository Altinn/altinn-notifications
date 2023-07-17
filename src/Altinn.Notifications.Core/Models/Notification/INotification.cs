using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Interface describing a base notification.
/// </summary>
public interface INotification
{
    /// <summary>
    /// Gets the id of the notification.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the order id of the notification.
    /// </summary>
    public string OrderId { get; }

    /// <summary>
    /// Gets the requested send time of the notification.
    /// </summary>
    public DateTime RequestedSendTime { get; }

    /// <summary>
    /// Gets the notifiction channel for the notification.
    /// </summary>
    public NotificationChannel NotificationChannel { get; }
}