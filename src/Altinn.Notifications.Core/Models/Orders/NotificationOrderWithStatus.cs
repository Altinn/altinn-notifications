using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// A class representing a registered notification order with status information. 
/// </summary>
public class NotificationOrderWithStatus
{
    /// <summary>
    /// Gets the id of the notification order
    /// </summary>
    public string Id { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the senders reference of the notification
    /// </summary>
    public string? SendersReference { get; internal set; }

    /// <summary>
    /// Gets the requested send time of the notification
    /// </summary>
    public DateTime RequestedSendTime { get; internal set; }

    /// <summary>
    /// Gets the short name of the creator of the notification order
    /// </summary>
    public Creator Creator { get; internal set; } = new(string.Empty);

    /// <summary>
    /// Gets the date and time of when the notification order was created
    /// </summary>
    public DateTime Created { get; internal set; }

    /// <summary>
    /// Gets the preferred notification channel of the notification order
    /// </summary>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <summary>
    /// Gets the processing status of the notication order
    /// </summary>
    public ProcessingStatus ProcessingStatus { get; internal set; } = new();

    /// <summary>
    /// Gets the summary of the notifiications statuses
    /// </summary>
    public NotificationsStatusSummary NotificationStatusSummary { get; internal set; } = new();
}

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class ProcessingStatus
{
    /// <summary>
    /// Gets the status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the description
    /// </summary>
    [JsonPropertyName("description")]
    public string StatusDescription { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the date time of when the status was last updated
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; internal set; }
}

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
public class NotificationsStatusSummary
{
    /// <summary>
    /// Gets the status of the email notifications
    /// </summary>
    public EmailNotificationStatus? Email { get; internal set; }
}

/// <summary>
/// A class representing a status overview for email notifications 
/// </summary>
public class EmailNotificationStatus : NotificationStatus
{
}

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public abstract class NotificationStatus
{
    /// <summary>
    /// Gets the number of generated notifications
    /// </summary>    
    [JsonPropertyName("generated")]
    public int Generated { get; internal set; }

    /// <summary>
    /// Gets the number of succeeeded notifications
    /// </summary>
    [JsonPropertyName("succeeded")]
    public int Succeeded { get; internal set; }
}