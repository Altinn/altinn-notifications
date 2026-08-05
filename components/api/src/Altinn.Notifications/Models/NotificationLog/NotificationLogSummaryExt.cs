using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.NotificationLog;

/// <summary>
/// Represents a notification log summary returned by the API.
/// </summary>
public record NotificationLogSummaryExt
{
    /// <summary>
    /// The identifier of the email or SMS notification this log entry was derived from.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// The Dialogporten dialog identifier associated with this notification, or <see langword="null"/> if not applicable.
    /// </summary>
    [JsonPropertyName("dialogId")]
    public string? DialogId { get; init; }

    /// <summary>
    /// The Dialogporten transmission identifier associated with this notification, or <see langword="null"/> if not applicable.
    /// </summary>
    [JsonPropertyName("transmissionId")]
    public string? TransmissionId { get; init; }

    /// <summary>
    /// The notification order type (e.g. <c>Notification</c>, <c>Reminder</c>, <c>Instant</c>, <c>Composed</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// The notification channel (<c>Email</c> or <c>Sms</c>).
    /// </summary>
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    /// <summary>
    /// The email address or mobile number the notification was sent to.
    /// </summary>
    [JsonPropertyName("destination")]
    public required string Destination { get; init; }

    /// <summary>
    /// The delivery result status at the time the log entry was created.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// The timestamp the order requested notifications be sent at.
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public required DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The timestamp when the notification status was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    public required DateTime LastUpdateTime { get; init; }
}
