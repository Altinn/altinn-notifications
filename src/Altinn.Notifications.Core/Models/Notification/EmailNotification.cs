using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an email notification and extends the <see cref="INotification">
/// </summary>
public class EmailNotification : INotification
{
    /// <inheritdoc/>
    public string Id { get; private set; }

    /// <inheritdoc/>
    public string OrderId { get; private set; }

    /// <inheritdoc/>
    public DateTime SendTime { get; private set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; private set; }

    /// <summary>
    /// Get or sets the content type of the email notification
    /// </summary>
    public EMailContentType ContentType { get; private set; }

    /// <summary>
    /// Get or sets the subject of the email notification
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>
    /// Get or sets the body of the email notification
    /// </summary>
    public string Body { get; private set; }

    /// <summary>
    /// Get or sets the from adress of the email notification
    /// </summary>
    public string FromAdress { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotification"/> class.
    /// </summary>
    public EmailNotification(string orderId, DateTime sendTime, NotificationChannel notificationChannel, EMailContentType contentType, string subject, string body, string fromAdress)
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