using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Represents a single notification (email or SMS) returned from a dashboard lookup.
/// </summary>
public record DashboardNotificationExt
{
    /// <summary>
    /// The unique identifier for the notification.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    /// <summary>
    /// The short name of the organisation that created the order.
    /// </summary>
    [JsonPropertyName("creatorName")]
    public string CreatorName { get; init; } = string.Empty;

    /// <summary>
    /// The Altinn resource the notification is related to.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>
    /// The sender's reference for the order.
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// When the notification was requested to be sent.
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The recipients of this notification.
    /// </summary>
    [JsonPropertyName("recipients")]
    public List<RecipientExt> Recipients { get; init; } = [];

    /// <summary>
    /// The delivery channel: "email" or "sms".
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// The delivery result status.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>
    /// When the result was recorded.
    /// </summary>
    [JsonPropertyName("resultTime")]
    public DateTime? ResultTime { get; init; }
}
