using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Represents an SMS notification that implements the <see cref="INotification{SmsNotificationResultType}"/> interface.
/// </summary>
public class SmsNotification : INotification<SmsNotificationResultType>
{
    /// <summary>
    /// Gets the unique identifier of the SMS notification.
    /// </summary>
    public Guid Id { get; internal set; }

    /// <summary>
    /// Gets the unique identifier of the order associated with this SMS notification.
    /// </summary>
    public Guid OrderId { get; internal set; }

    /// <summary>
    /// Gets the date and time when the SMS notification is requested to be sent.
    /// </summary>
    public DateTime RequestedSendTime { get; internal set; }

    /// <summary>
    /// Gets the communication channel through which the SMS notification will be sent.
    /// This is always <see cref="NotificationChannel.Sms"/>.
    /// </summary>
    public NotificationChannel NotificationChannel { get; } = NotificationChannel.Sms;

    /// <summary>
    /// Gets the recipient information for the SMS notification.
    /// </summary>
    public SmsRecipient Recipient { get; internal set; } = new();

    /// <summary>
    /// Gets the result of the SMS notification send operation.
    /// </summary>
    public NotificationResult<SmsNotificationResultType> SendResult { get; internal set; } = new(SmsNotificationResultType.New, DateTime.UtcNow);
}
