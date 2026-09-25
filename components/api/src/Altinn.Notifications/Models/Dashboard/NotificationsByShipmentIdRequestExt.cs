using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by shipment id.
/// </summary>
public class NotificationsByShipmentIdRequestExt : DashboardNotificationRequestExt
{
    /// <summary>
    /// The shipment id to look up.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "ShipmentId")]
    public required string ShipmentId { get; set; }
}
