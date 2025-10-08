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
    /// Get order details
    /// </summary>
    /// <remarks>
    /// Endpoint for retrieving details of a notification order, including its template and recipients.
    /// </remarks>
    /// <param name="id">The unique identifier of the notification order for which details are to be retrieved.</param>
    /// <returns>The details of the notification order were successfully retrieved.</returns>
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
    [HttpGet]
    [Route("{id}")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The details of the notification order were successfully retrieved.", typeof(NotificationOrderExt))]
    [SwaggerResponse(404, "No order matching the provided ID was found.")]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
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
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
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
    /// Get order status
    /// </summary>
    /// <remarks>
    /// Endpoint for retrieving the processing status of a notification order, along with a summary of all generated notifications.
    /// </remarks>
    /// <param name="id">The unique identifier of the notification order for which status are to be retrieved.</param>
    /// <returns>The status of the notification order was successfully retrieved.</returns>
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
    [HttpGet]
    [Route("{id}/status")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The status of the notification order was successfully retrieved.", typeof(NotificationOrderWithStatusExt))]
    [SwaggerResponse(404, "No order matching the provided ID was found.")]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
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
    /// Send notifications
    /// </summary>
    /// <remarks>
    /// Endpoint for sending a notification via a selected notification channel to one or more recipient.
    /// </remarks>
    /// <returns>The notification order request response</returns>
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The request was accepted and a notification order has been successfully generated.", typeof(NotificationOrderRequestResponseExt))]
    [SwaggerResponse(400, "The request was invalid.", typeof(ValidationProblemDetails))]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
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
    /// <remarks>
    /// Endpoint for stopping the sending of a registered notification order.
    /// </remarks>
    /// <param name="id">The unique identifier of the notification order for which notifications are to be cancelled.</param>
    /// <returns>The cancelled notification order</returns>
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
    [HttpPut]
    [Route("{id}/cancel")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The notification order was cancelled. No notifications will be sent.", typeof(NotificationOrderWithStatusExt))]
    [SwaggerResponse(404, "No order matching the provided ID was found.")]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
    [SwaggerResponse(409, "The order cannot be cancelled due to current processing status.")]
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
