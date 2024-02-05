using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
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
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
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
    /// Endpoint for retrieving an order by id.
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
        string? expectedCreator = HttpContext.GetOrg();

        if (expectedCreator == null)
        {
            return Forbid();
        }

        Result<NotificationOrder, ServiceError> result = await _getOrderService.GetOrderById(id, expectedCreator);

        return result.Match<ActionResult<NotificationOrderExt>>(
             order =>
             {
                 return order.MapToNotificationOrderExt();
             },
             error => StatusCode(error.ErrorCode, error.ErrorMessage));
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
        string? expectedCreator = HttpContext.GetOrg();
        if (expectedCreator == null)
        {
            return Forbid();
        }

        Result<List<NotificationOrder>, ServiceError> result = await _getOrderService.GetOrdersBySendersReference(sendersReference, expectedCreator);

        return result.Match<ActionResult<NotificationOrderListExt>>(
             orders =>
             {
                 return orders.MapToNotificationOrderListExt();
             },
             error => StatusCode(error.ErrorCode, error.ErrorMessage));
    }

    /// <summary>
    /// Endpoint for retrieving an order with processing and notificatio status by id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <returns>The order that correspons to the provided id</returns>
    [HttpGet]
    [Route("{id}/status")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order matching the provided id was retrieved successfully", typeof(NotificationOrderWithStatusExt))]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public async Task<ActionResult<NotificationOrderWithStatusExt>> GetWithStatusById([FromRoute] Guid id)
    {
        string? expectedCreator = HttpContext.GetOrg();
        if (expectedCreator == null)
        {
            return Forbid();
        }

        Result<NotificationOrderWithStatus, ServiceError> result = await _getOrderService.GetOrderWithStatuById(id, expectedCreator);

        return result.Match<ActionResult<NotificationOrderWithStatusExt>>(
         order =>
         {
             return order.MapToNotificationOrderWithStatusExt();
         },
         error => StatusCode(error.ErrorCode, error.ErrorMessage));
    }
}
