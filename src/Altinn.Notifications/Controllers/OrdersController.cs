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
[Route("notifications/api/v1/orders")]
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
        string? expectedCreator = User.GetOrg();
        if (expectedCreator == null)
        {
            return Forbid();
        }

        var (order, error) = await _orderService.GetOrderById(id, expectedCreator);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        var orderExt = order.MapToNotificationOrderExt();
        orderExt.SetResourceLinks();

        return orderExt;
    }

    /// <summary>
    /// Endpoint for retrieving an order by senders reference
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <returns>The order that correspons to the provided senders reference</returns>
    [HttpGet]
    public async Task<ActionResult<NotificationOrderListExt>> GetBySendersRef([FromQuery] string sendersReference)
    {
        if (string.IsNullOrEmpty(sendersReference))
        {
            return BadRequest();
        }

        string? expectedCreator = User.GetOrg();
        if (expectedCreator == null)
        {
            return Forbid();
        }

        var (orders, error) = await _orderService.GetOrdersBySendersReference(sendersReference, expectedCreator);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        var orderList = orders.MapToNotificationOrderListExt();
        orderList.SetResourceLinks();
        return orderList;
    }
}