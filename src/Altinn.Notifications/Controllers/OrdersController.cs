using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to notification orders
/// </summary>
[Route("notifications/api/v1/orders/email")]
[ApiController]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersController"/> class.
    /// </summary>
    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Endpoint for retrieving an order by id
    /// </summary>
    /// <param name="orderId">The order id</param>
    /// <returns>The order that correspons to the provided id</returns>
    [HttpGet]
    [Route("{orderId}")]
    public async Task<ActionResult<NotificationOrderExt>> Get([FromRoute] Guid orderId)
    {
        var res = await _orderService.GetOrderById(orderId);
        return HandleServiceResult(res.Order, res.Error);
    }

    /// <summary>
    /// Endpoint for retrieving an order by senders reference
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <returns>The order that correspons to the provided senders reference</returns>
    [HttpGet]
    public async Task<ActionResult<NotificationOrderExt>> Get([FromQuery] string sendersReference)
    {
        if (string.IsNullOrEmpty(sendersReference))
        {
            return BadRequest();
        }

        var res = await _orderService.GetOrderBySendersReference(sendersReference);
        return HandleServiceResult(res.Order, res.Error);
    }

    private ActionResult<NotificationOrderExt> HandleServiceResult(NotificationOrder? order, ServiceError? error)
    {
        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        // basic authorization check to verify caller has access to order
        string? requestingOrg = User.GetOrg();

        if (requestingOrg != order!.Creator.ShortName)
        {
            return Forbid();
        }

        return order.MapToNotificationOrderExt();
    }
}