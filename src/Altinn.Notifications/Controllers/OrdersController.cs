using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to notification orders
/// </summary>
[Route("notifications/api/v1/orders")]
[ApiController]
[Authorize]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
public class OrdersController : ControllerBase
{
    private readonly IGetOrderService _getOrderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersController"/> class.
    /// </summary>
    public OrdersController(IGetOrderService getOrderService)
    {
        _getOrderService = getOrderService;
    }

    /// <summary>
    /// Endpoint for retrieving an order by id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <returns>The order that correspons to the provided id</returns>
    [HttpGet]
    [Route("{id}")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully", typeof(NotificationOrderExt))]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public async Task<ActionResult<NotificationOrderExt>> GetById([FromRoute] Guid id)
    {
        string? expectedCreator = HttpContext.Items["Org"] as string;

        if (expectedCreator == null)
        {
            return Forbid();
        }

        var (order, error) = await _getOrderService.GetOrderById(id, expectedCreator);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        return order!.MapToNotificationOrderExt();
    }

    /// <summary>
    /// Endpoint for retrieving an order by senders reference
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <returns>The order that correspons to the provided senders reference</returns>
    [HttpGet]
    [Produces("application/json")]
    [SwaggerResponse(200, "The list of notification orders matching the provided senders ref was retrieved successfully", typeof(NotificationOrderListExt))]
    public async Task<ActionResult<NotificationOrderListExt>> GetBySendersRef([FromQuery, BindRequired] string sendersReference)
    {
        string? expectedCreator = HttpContext.Items["Org"] as string;
        if (expectedCreator == null)
        {
            return Forbid();
        }

        var (orders, error) = await _getOrderService.GetOrdersBySendersReference(sendersReference, expectedCreator);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        return orders!.MapToNotificationOrderListExt();
    }

    /// <summary>
    /// Endpoint for retrieving an order with processing and notificatio status by id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <returns>The order that correspons to the provided id</returns>
    [HttpGet]
    [Route("{id}/status")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully", typeof(NotificationOrderExt))]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public async Task<ActionResult<NotificationOrderWithStatusExt>> GetWithStatusById([FromRoute] Guid id)
    {
        string? expectedCreator = HttpContext.Items["Org"] as string;
        if (expectedCreator == null)
        {
            return Forbid();
        }

        var (order, error) = await _getOrderService.GetOrderWithStatuById(id, expectedCreator);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        return order!.MapToNotificationOrderWithStatusExt();
    }
}
