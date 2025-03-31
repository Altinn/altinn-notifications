using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

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
    private readonly IValidator<NotificationOrderRequestExt> _validator;
    private readonly IGetOrderService _getOrderService;
    private readonly IOrderRequestService _orderRequestService;
    private readonly ICancelOrderService _cancelOrderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersController"/> class.
    /// </summary>
    public OrdersController(IValidator<NotificationOrderRequestExt> validator, IGetOrderService getOrderService, IOrderRequestService orderRequestService, ICancelOrderService cancelOrderService)
    {
        _validator = validator;
        _getOrderService = getOrderService;
        _orderRequestService = orderRequestService;
        _cancelOrderService = cancelOrderService;
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

    /// <summary>
    /// Add a notification order.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after som basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The notification order was accepted", typeof(NotificationOrderRequestResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponseHeader(202, "Location", "string", "Link to access the newly created notification order.")]
    public async Task<ActionResult<NotificationOrderRequestResponseExt>> Post(NotificationOrderRequestExt notificationOrderRequest)
    {
        var validationResult = _validator.Validate(notificationOrderRequest);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(this.ModelState);
            return ValidationProblem(ModelState);
        }

        string? creator = HttpContext.GetOrg();

        if (creator == null)
        {
            return Forbid();
        }

        var orderRequest = notificationOrderRequest.MapToOrderRequest(creator);
        NotificationOrderRequestResponse result = await _orderRequestService.RegisterNotificationOrder(orderRequest);

        return Accepted(result.OrderId!.GetSelfLinkFromOrderId(), result.MapToExternal());
    }

    /// <summary>
    /// Cancel a notification order.
    /// </summary>
    /// <param name="id">The id of the order to cancel.</param>
    /// <returns>The cancelled notification order</returns>
    [HttpPut]
    [Route("{id}/cancel")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order was cancelled. No notifications will be sent.", typeof(NotificationOrderWithStatusExt))]
    [SwaggerResponse(409, "The order cannot be cancelled due to current processing status")]
    [SwaggerResponse(404, "No order with the provided id was found")]
    public async Task<ActionResult<NotificationOrderWithStatusExt>> CancelOrder([FromRoute] Guid id)
    {
        string? expectedCreator = HttpContext.GetOrg();

        if (expectedCreator == null)
        {
            return Forbid();
        }

        Result<NotificationOrderWithStatus, CancellationError> result = await _cancelOrderService.CancelOrder(id, expectedCreator);

        return result.Match(
         order =>
         {
             return order.MapToNotificationOrderWithStatusExt();
         },
         error =>
         {
             return error switch
             {
                 CancellationError.OrderNotFound => (ActionResult<NotificationOrderWithStatusExt>)NotFound(),
                 CancellationError.CancellationProhibited => (ActionResult<NotificationOrderWithStatusExt>)Conflict(),
                 _ => (ActionResult<NotificationOrderWithStatusExt>)StatusCode(500),
             };
         });
    }
}
