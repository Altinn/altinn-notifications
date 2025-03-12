using Altinn.Notifications.Models;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle notification orders that has one or more reminders.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/orders")]
public class FutureOrdersController
{
    /// <summary>
    /// Retrieves notification order.
    /// </summary>
    /// <param name="notificationOrderId">The notification order identifier.</param>
    /// <returns></returns>
    [HttpGet]
    [Route("{notificationOrderId}")]
    [Produces("application/json")]
    [SwaggerResponse(404, "No order with the provided id was not found")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully")]
    public async Task<ActionResult<NotificationOrderReminderResponseExt>> GetById(Guid notificationOrderId)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new notification order that has one or more reminders.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after some basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <param name="notificationOrderRequest">The notification order with reminders request</param>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(200, "The notification order was created.", typeof(NotificationOrderReminderResponseExt))]
    [SwaggerResponse(201, "The notification order was created.", typeof(NotificationOrderReminderResponseExt))]
    public async Task<ActionResult<NotificationOrderReminderResponseExt>> Post(NotificationOrderWithRemindersRequestExt notificationOrderRequest)
    {
        throw new NotImplementedException();
    }
}
