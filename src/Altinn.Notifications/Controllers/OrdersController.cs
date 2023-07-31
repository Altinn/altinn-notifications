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
    /// <param name="id">The order id</param>
    /// <returns>The order that correspons to the provided id</returns>
    [HttpGet]
    [Route("{id}")]
    public async Task<ActionResult<NotificationOrderExt>> GetById([FromRoute] Guid id)
    {
        var (order, error) = await _orderService.GetOrderById(id);
        return HandleServiceResult(order, error);
    }

    /// <summary>
    /// Endpoint for retrieving an order by senders reference
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <returns>The order that correspons to the provided senders reference</returns>
    [HttpGet]
    public async Task<ActionResult<NotificationOrderExt>> GetBySendersRef([FromQuery] string sendersReference)
    {
        if (string.IsNullOrEmpty(sendersReference))
        {
            return BadRequest();
        }

        var (order, error) = await _orderService.GetOrderBySendersReference(sendersReference);
        return HandleServiceResult(order, error);
    }

    /// <summary>
    /// Processes the output from the service result
    /// </summary>
    internal ActionResult<NotificationOrderExt> HandleServiceResult(NotificationOrder? order, ServiceError? error)
    {
        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        // basic authorization check to verify caller has access to order
        if (User.GetOrg() != order!.Creator.ShortName)
        {
            return Forbid();
        }

        return order.MapToNotificationOrderExt();
    }
}