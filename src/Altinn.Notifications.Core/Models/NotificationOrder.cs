using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing a notification order
/// </summary>
public class NotificationOrder
{
    /// <summary>
    /// Gets or sets the senders reference of a notification
    /// </summary>
    public string? SendersReference { get; set; } // internal set? is it possible to set /modify this after creation?

    /// <summary>
    /// Gets or sets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; set; }

    /// <summary>
    /// Gets or sets the send time for the notification(s)
    /// </summary>
    public DateTime SendTime { get; set; }

    /// <summary>
    /// Gets or sets the preferred notification channel
    /// </summary>
    public NotificationChannelPreferred PreferredNotificationChannel { get; set; }

    /// <summary>
    /// Gets or sets the creator of the notification
    /// </summary>
    public Creator Creator { get; set; }

    /// <summary>
    /// Gets or sets a list of recipients
    /// </summary>
    public List<Recipient>? Recipients { get; set; }
}