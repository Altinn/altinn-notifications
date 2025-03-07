using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle notification orders that has one or more reminders.
/// </summary>
[ApiController]
public class OrderWithRemindersStatusController : ControllerBase
{
    /// <summary>
    /// Gets the shipment.
    /// </summary>
    /// <param name="notificationOrderId">The notification order identifier.</param>
    [HttpGet]
    [Route("notifications/api/v1/orderswithremindersstatus/{notificationOrderId}/status/shipment")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully")]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public IActionResult GetShipment(Guid notificationOrderId)
    {
        throw new NotImplementedException();
    }
}
