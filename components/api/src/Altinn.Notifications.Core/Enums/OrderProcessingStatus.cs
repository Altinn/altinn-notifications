namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Defines the processing states of a notification order as it moves through the its lifecycle.
/// </summary>
public enum OrderProcessingStatus
{
    /// <summary>
    /// The notification order has been received and registered in the system, but processing has not yet begun.
    /// </summary>
    Registered,

    /// <summary>
    /// The notification order is currently being processed by the system.
    /// </summary>
    Processing,

    /// <summary>
    /// The notification order has been successfully processed by the system.
    /// </summary>
    Completed,

    /// <summary>
    /// The notification order was not sent because the send condition was not met.
    /// </summary>
    SendConditionNotMet,

    /// <summary>
    /// The notification order has been explicitly cancelled and will not be processed further.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The notification order has been processed, but its final delivery status is pending.
    /// </summary>
    Processed
}
