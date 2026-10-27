namespace Altinn.Notifications.Core.Models.NotificationLog;

/// <summary>
/// Represents a notification log summary returned when querying the notification log.
/// </summary>
public record NotificationLogSummary
{
    /// <summary>
    /// The identifier of the email or SMS notification this log entry was derived from.
    /// </summary>
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// The Dialogporten dialog identifier associated with this notification, or <see langword="null"/> when
    /// the order has no Dialogporten association.
    /// </summary>
    public string? DialogId { get; init; }

    /// <summary>
    /// The Dialogporten transmission identifier associated with this notification, or <see langword="null"/>
    /// when the order has no Dialogporten association.
    /// </summary>
    public string? TransmissionId { get; init; }

    /// <summary>
    /// The notification order type (e.g. <c>Notification</c>, <c>Reminder</c>, <c>Instant</c>, <c>Composed</c>).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The notification channel (<c>Email</c> or <c>Sms</c>).
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// The email address or mobile number the notification was sent to.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// The delivery result status at the time the log entry was created.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The timestamp the order requested notifications be sent at.
    /// </summary>
    public required DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The timestamp when the notification status was last updated.
    /// </summary>
    public required DateTime LastUpdateTime { get; init; }
}
