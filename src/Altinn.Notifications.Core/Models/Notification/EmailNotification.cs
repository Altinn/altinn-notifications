using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Class describing an email notification and extends the <see cref="INotification">
/// </summary>
public class EmailNotification : INotification
{
    /// <inheritdoc/>
    public string? Id { get; set; }

    /// <inheritdoc/>
    public string OrderId { get; set; }

    /// <inheritdoc/>
    public DateTime SendTime { get; set; }

    /// <inheritdoc/>
    public NotificationChannel NotificationChannel { get; set; }

    /// <summary>
    /// Get or sets the content type of the email notification
    /// </summary>
    public EMailContentType ContentType { get; set; }

    /// <summary>
    /// Get or sets the subject of the email notification
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Get or sets the body of the email notification
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Get or sets the from adress of the email notification
    /// </summary>
    public string FromAdress { get; set; }
}