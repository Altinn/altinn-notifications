using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Defines the contract for a base notification.
/// </summary>
/// <typeparam name="TEnum">The type of the enumeration used to represent the notification status.</typeparam>
public interface INotification<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets the unique identifier of the notification.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the unique identifier of the order associated with the notification.
    /// </summary>
    Guid OrderId { get; }

    /// <summary>
    /// Gets the date and time when the notification is requested to be sent.
    /// </summary>
    DateTime RequestedSendTime { get; }

    /// <summary>
    /// Gets the communication channel through which the notification will be sent.
    /// </summary>
    NotificationChannel NotificationChannel { get; }

    /// <summary>
    /// Gets the result of the notification send operation.
    /// </summary>
    NotificationResult<TEnum> SendResult { get; }
}
