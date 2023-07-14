using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an email notification and extends the <see cref="INotification"/>
/// </summary>
public class EmailNotification : INotification
{
    /// <inheritdoc/>
    public string Id { get; internal set; }

    /// <inheritdoc/>
    public string OrderId { get; internal set; }

    /// <inheritdoc/>
    public DateTime SendTime { get; internal set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <summary>
    /// Get or sets the content type of the email notification
    /// </summary>
    public EmailContentType ContentType { get; internal set; }

    /// <summary>
    /// Get or sets the subject of the email notification
    /// </summary>
    public string Subject { get; internal set; }

    /// <summary>
    /// Get or sets the body of the email notification
    /// </summary>
    public string Body { get; internal set; }

    /// <summary>
    /// Get or sets the from adress of the email notification
    /// </summary>
    public string FromAdress { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    public EmailNotification(string orderId, DateTime sendTime, NotificationChannel notificationChannel, EmailContentType contentType, string subject, string body, string fromAdress)
    {
        Id = Guid.NewGuid().ToString();
        OrderId = orderId;
        SendTime = sendTime;
        NotificationChannel = notificationChannel;
        ContentType = contentType;
        Subject = subject;
        Body = body;
        FromAdress = fromAdress;
    }
}