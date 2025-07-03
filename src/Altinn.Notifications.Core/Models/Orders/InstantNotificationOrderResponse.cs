namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the response model for a request to send an SMS notification immediately.
/// </summary>
public class InstantNotificationOrderResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for notification order.
    /// </summary>
    public required Guid ShipmentId { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference identifier.
    /// </summary>
    public string? SendersReference { get; set; }
}
