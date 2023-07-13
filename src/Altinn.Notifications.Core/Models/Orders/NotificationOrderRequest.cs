using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Class representing a notification order request
/// </summary>
public class NotificationOrderRequest
{
    /// <summary>
    /// Gets the senders reference of a notification
    /// </summary>
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; set; }

    /// <summary>
    /// Gets the send time for the notification(s)
    /// </summary>
    public DateTime SendTime { get; set; }

    /// <summary>
    /// Gets the preferred notification channel
    /// </summary>
    public NotificationChannel NotificationChannel { get; set; }

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public NotificationOrderRequest(string? sendersReference, List<INotificationTemplate> templates, DateTime sendTime, NotificationChannel notificationChannel, List<Recipient> recipients)
    {
        SendersReference = sendersReference;
        Templates = templates;
        SendTime = sendTime;
        NotificationChannel = notificationChannel;
        Recipients = recipients;
    }
}