namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Defines the different types of notification orders that determine their processing path and scheduling priority.
/// </summary>
/// <remarks>
/// The order type controls how notifications are processed through the system pipeline:
/// - Standard notifications and reminders follow the scheduled processing via cronjobs and topics
/// - Instant bypass the standard queuing mechanism for immediate delivery
/// </remarks>
public enum OrderType : uint
{
    /// <summary>
    /// Represents a standard notification order processed through the scheduled pipeline.
    /// </summary>
    /// <remarks>
    /// Standard notifications are stored in the database and picked up by a cronjob after approximately 
    /// 5 minutes, then published to a topic for further processing and delivery.
    /// </remarks>
    Notification = 0,

    /// <summary>
    /// Represents a reminder order that is sent as a follow-up to a previous notification.
    /// </summary>
    /// <remarks>
    /// Like standard notifications, reminders are stored in the database and processed through 
    /// the scheduled cronjob pipeline. They are typically sent after a specified time has elapsed 
    /// since the original notification to remind recipients of pending actions or information.
    /// </remarks>
    Reminder = 1,

    /// <summary>
    /// Represents a high-priority notification requiring immediate delivery.
    /// </summary>
    /// <remarks>
    /// Instant bypass the queuing and scheduling mechanisms. When received, 
    /// they are immediately saved to the database and directly forwarded to the appropriate 
    /// delivery service (SMS or Email) without waiting for cronjob processing.
    /// </remarks>
    Instant = 2
}
