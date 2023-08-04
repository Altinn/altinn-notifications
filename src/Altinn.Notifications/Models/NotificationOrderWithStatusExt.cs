using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a registered notification order with status information. 
/// </summary>
/// <remarks>
/// External representation to be used in the API.
/// </remarks>
public class NotificationOrderWithStatusExt
{
    /// <summary>
    /// Gets or sets the id of the notification order
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the senders reference of the notification
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the requested send time of the notification
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; set; }

    /// <summary>
    /// Gets or sets the short name of the creator of the notification order
    /// </summary>
    [JsonPropertyName("creator")]
    public string Creator { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time of when the notification order was created
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the preferred notification channel of the notification order
    /// </summary>
    [JsonPropertyName("notificationChannel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationChannel NotificationChannel { get; set; }

    /// <summary>
    /// Gets or sets the processing status of the notication order
    /// </summary>
    [JsonPropertyName("processingStatus")]
    public ProcessingStatusExt ProcessingStatus { get; set; } = new();

    /// <summary>
    /// Gets or sets the summary of the notifiications statuses
    /// </summary>
    [JsonPropertyName("notificationsStatusSummary")]
    public NotificationsStatusSummaryExt NotificationStatusSummary { get; set; } = new();
}