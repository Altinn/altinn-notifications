using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing a notification order
/// </summary>
public class NotificationOrder
{
    /// <summary>
    /// Gets the id of the notification order
    /// </summary>
    public string Id { get; private set;  }

    /// <summary>
    /// Gets the senders reference of a notification
    /// </summary>
    public string? SendersReference { get; private set; } 

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; private set; }

    /// <summary>
    /// Gets the send time for the notification(s)
    /// </summary>
    public DateTime SendTime { get; set; }

    /// <summary>
    /// Gets the preferred notification channel
    /// </summary>
    public NotificationChannelPreferred PreferredNotificationChannel { get; private set; }

    /// <summary>
    /// Gets the creator of the notification
    /// </summary>
    public Creator Creator { get; private set; }

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrder"/> class.
    /// </summary>
    public NotificationOrder(string? sendersReference, List<INotificationTemplate> templates, DateTime sendTime, NotificationChannelPreferred preferredNotificationChannel, Creator creator, List<Recipient> recipients)
    {
        Id = Guid.NewGuid().ToString();
        SendersReference = sendersReference;
        Templates = templates;
        SendTime = sendTime;
        PreferredNotificationChannel = preferredNotificationChannel;
        Creator = creator;
        Recipients = recipients;
    }
}