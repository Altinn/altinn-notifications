using Altinn.Notifications.Core.Enums;

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
    /// Get the id of the recipient of the sms notification
    /// </summary>
    public string? RecipientId { get; internal set; }

    /// <summary>
    /// Get or sets the mobilenumber of the sms notification
    /// </summary>
    public string MobileNumber { get; internal set; } = string.Empty;

    /// <inheritdoc/>
    public NotificationResult<SmsNotificationResultType> SendResult { get; internal set; } = new(SmsNotificationResultType.New, DateTime.UtcNow);
}
