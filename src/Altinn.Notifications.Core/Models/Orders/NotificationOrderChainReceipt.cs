namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the complete receipt for a notification order chain creation operation.
/// </summary>
/// <remarks>
/// This class extends <see cref="NotificationOrderChainShipment"/> to include information about
/// any reminders that were configured as part of the notification chain. It serves as a comprehensive
/// confirmation that includes both the primary notification and all its associated reminders,
/// providing clients with tracking information for the entire notification sequence.
/// </remarks>
public class NotificationOrderChainReceipt : NotificationOrderChainShipment
{
    /// <summary>
    /// Gets or sets the reminders associated with this notification order.
    /// </summary>
    /// <remarks>
    /// Contains tracking information for all reminder that were created
    /// as part of this notification chain. Each reminder entry provides identifiers that
    /// can be used to track or reference the specific reminder shipment.
    /// </remarks>
    public List<NotificationOrderChainShipment>? Reminders { get; set; }
}
