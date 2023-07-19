using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an email notification and extends the <see cref="INotification{EmailNotificationResultType}"/>
/// </summary>
public class EmailNotification : INotification<EmailNotificationResultType>
{
    /// <inheritdoc/>
    public string Id { get; internal set; }

    /// <inheritdoc/>
    public string OrderId { get; internal set; }

    /// <inheritdoc/>
    public DateTime RequestedSendTime { get; internal set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; } = NotificationChannel.Email;

    /// <summary>
    /// Get the id of the recipient of the email notification
    /// </summary>
    public string? RecipientId { get; internal set; }

    /// <summary>
    /// Get or sets the to address of the email notification
    /// </summary>
    public string ToAddress { get; internal set; } = string.Empty;

    /// <summary>
    /// Get or sets the send result of
    /// </summary>
    public NotificationResult<EmailNotificationResultType> SendResult { get; internal set; } = new(EmailNotificationResultType.New);

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    public EmailNotification(string orderId, DateTime sendTime)
    {
        Id = Guid.NewGuid().ToString();
        OrderId = orderId;
        RequestedSendTime = sendTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    internal EmailNotification()
    {
        Id = string.Empty;
        OrderId = string.Empty;
        RequestedSendTime = DateTime.MinValue;
    }
}