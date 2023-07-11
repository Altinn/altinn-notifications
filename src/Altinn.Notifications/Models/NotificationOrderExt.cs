using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a registered notification order. 
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationOrderExt
{
    /// <summary>
    /// Gets or sets the id of the notification order
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of recipients
    /// </summary>
    public List<RecipientExt> Recipients { get; set; } = new List<RecipientExt>();

    /// <summary>
    /// Gets or sets the templates to use as the base the notification
    /// </summary>
    public List<INotificationTemplate> Templates { get; set; } = new List<INotificationTemplate>();

    /// <summary>
    /// Gets or sets the senders reference of the notification
    /// </summary>
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the requested send time of the notification
    /// </summary>
    public DateTime SendTime { get; set; }

    /// <summary>
    /// Gets or sets the date and time of when the notification order was created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the preferred notification channel of the notification order
    /// </summary>
    public NotificationChannelPreferred NotificationChannelPreferred { get; set; }

    /// <summary>
    /// Gets or sets the creator of the notification order
    /// </summary>
    public CreatorExt Creator { get; set; } = new CreatorExt();
}