using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle notification orders that has one or more reminders.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/orders")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class FutureOrdersController : ControllerBase
{
    private readonly IOrderRequestService _orderRequestService;
    private readonly IValidator<NotificationOrderChainRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FutureOrdersController"/> class.
    /// </summary>
    public FutureOrdersController(
        IOrderRequestService orderRequestService,
        IValidator<NotificationOrderChainRequestExt> validator)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
    }

    /// <summary>
    /// Creates a new notification order that has one or more reminders.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after some basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <param name="notificationOrderRequest">The notification order request</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The notification order was created.", typeof(NotificationOrderChainResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(NotificationOrderChainResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    public async Task<ActionResult<NotificationOrderChainResponseExt>> Post(NotificationOrderChainRequestExt notificationOrderRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = _validator.Validate(notificationOrderRequest);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            string? creator = HttpContext.GetOrg();
            if (creator == null)
            {
                return Forbid();
            }

            var orderChainTracking = await _orderRequestService.RetrieveOrderChainTracking(creator, notificationOrderRequest.IdempotencyId, cancellationToken);
            if (orderChainTracking != null)
            {
                return Ok(orderChainTracking.MapToNotificationOrderChainResponseExt());
            }

            var notificationOrderChainRequest = notificationOrderRequest.MapToNotificationOrderChainRequest(creator);

            Result<NotificationOrderChainResponse, ServiceError> result = await _orderRequestService.RegisterNotificationOrderChain(notificationOrderChainRequest, cancellationToken);

            return result.Match(
                registeredNotificationOrderChain =>
                {
                    return Created(registeredNotificationOrderChain.OrderChainId.GetSelfLinkFromOrderChainId(), registeredNotificationOrderChain.MapToNotificationOrderChainResponseExt());
                },
                error =>
                {
                    var problemDetails = new ProblemDetails
                    {
                        Title = "Notification order chain registration failed",
                        Detail = error.ErrorMessage,
                        Status = error.ErrorCode
                    };
                    return StatusCode(error.ErrorCode, problemDetails);
                });
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = 400,
                Detail = ex.Message,
                Title = "Invalid notification order request"
            };
            return StatusCode(400, problemDetails);
        }
        catch (OperationCanceledException)
        {
            var problemDetails = new ProblemDetails
            {
                Title = "Request terminated",
                Detail = "The client disconnected or cancelled the request before the server could complete processing.",
                Status = 499
            };

            return StatusCode(499, problemDetails);
        }
    }
}
