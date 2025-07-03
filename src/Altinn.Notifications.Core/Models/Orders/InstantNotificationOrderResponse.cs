namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the response model for a request to send an SMS notification immediately.
/// </summary>
public class InstantNotificationOrderResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for notification order.
    /// </summary>
    public required NotificationOrderChainShipment Notification { get; set; }
}
