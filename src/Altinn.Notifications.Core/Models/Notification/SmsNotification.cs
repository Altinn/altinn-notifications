using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an sns notification and extends the <see cref="INotification{SmsNotificationResultType}"/>
/// </summary>
public class SmsNotification : INotification<SmsNotificationResultType>
{
    /// <inheritdoc/>
    public Guid Id { get; internal set; }

    /// <inheritdoc/>
    public Guid OrderId { get; internal set; }

    /// <inheritdoc/>
    public DateTime RequestedSendTime { get; internal set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; } = NotificationChannel.Sms;

    /// <summary>
    /// Get the recipient of the notification
    /// </summary>
    public SmsRecipient Recipient { get; internal set; } = new();

    /// <inheritdoc/>
    public NotificationResult<SmsNotificationResultType> SendResult { get; internal set; } = new(SmsNotificationResultType.New, DateTime.UtcNow);
}
