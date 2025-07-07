namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Defines the different types of an order.
/// </summary>
public enum OrderType : uint
{
    /// <summary>
    /// Represents a standard notification order that should be processed according to normal scheduling rules.
    /// </summary>
    Notification = 0,

    /// <summary>
    /// Represents a reminder order that is sent as a follow-up to a previous notification.
    /// </summary>
    Reminder = 1,

    /// <summary>
    /// Represents a notification order intended for immediate processing, bypassing all processing queuing mechanisms
    /// </summary>
    Instant = 2
}
