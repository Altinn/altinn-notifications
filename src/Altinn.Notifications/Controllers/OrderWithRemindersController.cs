using Altinn.Notifications.Models;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle notification orders that has one or more reminders.
/// </summary>
[ApiController]
[Route("notifications/api/v1/orderswithreminders")]
public class OrderWithRemindersController
{
    /// <summary>
    /// Retrieves notification order.
    /// </summary>
    /// <param name="notificationOrderId">The notification order identifier.</param>
    /// <returns></returns>
    [HttpGet]
    [Route("{notificationOrderId}")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully")]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public IActionResult GetById(Guid notificationOrderId)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new notification order that has one or more reminders .
    /// </summary>
    /// <remarks>
    /// The API will accept the request after some basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The notification order was accepted")]
    [SwaggerResponse(400, "The notification order is invalid")]
    [SwaggerResponseHeader(202, "Location", "string", "Link to access the newly created notification order.")]
    public async Task<IActionResult> Post(NotificationOrderWithRemindersRequestExt notificationOrderRequest)
    {
        throw new NotImplementedException();
    }
}
