namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the tracking information for an instant notification order.
/// </summary>
public record InstantNotificationOrderTracking
{
    /// <summary>
    /// The unique identifier for the notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// The unique identifier for notification order shipment.
    /// </summary>
    public required NotificationOrderChainShipment Notification { get; init; }
}
