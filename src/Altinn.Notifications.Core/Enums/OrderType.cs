namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Defines the different types of an order.
/// </summary>
public enum OrderType : uint
{
    /// <summary>
    /// Represents a notification order that should be initiated immediately.
    /// </summary>
    Notification = 0,

    /// <summary>
    /// Represents a reminder order that should be initiated after the main order.
    /// </summary>
    Reminder = 1
}
