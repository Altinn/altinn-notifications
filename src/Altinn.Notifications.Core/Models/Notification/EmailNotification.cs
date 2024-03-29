﻿using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an email notification and extends the <see cref="INotification{EmailNotificationResultType}"/>
/// </summary>
public class EmailNotification : INotification<EmailNotificationResultType>
{
    /// <inheritdoc/>
    public Guid Id { get; internal set; }

    /// <inheritdoc/>
    public Guid OrderId { get; internal set; }

    /// <inheritdoc/>
    public DateTime RequestedSendTime { get; internal set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; } = NotificationChannel.Email;

    /// <summary>
    /// Gets the recipient of the notification
    /// </summary>
    public EmailRecipient Recipient { get; internal set; } = new();

    /// <summary>
    /// Get or sets the send result of the notification
    /// </summary>
    public NotificationResult<EmailNotificationResultType> SendResult { get; internal set; } = new(EmailNotificationResultType.New, DateTime.UtcNow);

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    public EmailNotification(Guid orderId, DateTime sendTime)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        RequestedSendTime = sendTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    internal EmailNotification()
    {
        Id = Guid.Empty;
        OrderId = Guid.Empty;
        RequestedSendTime = DateTime.MinValue;
    }
}
