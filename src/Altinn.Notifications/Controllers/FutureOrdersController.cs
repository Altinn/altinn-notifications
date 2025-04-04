using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
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
    public FutureOrdersController(IOrderRequestService orderRequestService, IValidator<NotificationOrderChainRequestExt> validator)
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
    /// <param name="notificationOrderRequest">The notification order with reminders request</param>
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
    [SwaggerResponse(500, "An unexpected error occurred")]
    public async Task<ActionResult<NotificationOrderChainResponseExt>> Post(NotificationOrderChainRequestExt notificationOrderRequest, CancellationToken cancellationToken = default)
    {
        try
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

            var orderChainTracking = await _orderRequestService.RetrieveOrderChainTracking(creator, notificationOrderRequest.IdempotencyId, cancellationToken);
            if (orderChainTracking != null)
            {
                return Ok(orderChainTracking.MapToNotificationOrderChainResponseExt());
            }

            var notificationOrderChainRequest = notificationOrderRequest.MapToNotificationOrderChainRequest(creator);
            var registeredNotificationOrderChain = await _orderRequestService.RegisterNotificationOrderChain(notificationOrderChainRequest, cancellationToken);

            return Accepted(registeredNotificationOrderChain.OrderChainId.GetSelfLinkFromOrderChainId(), registeredNotificationOrderChain.MapToNotificationOrderChainResponseExt());
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing");
        }
        catch (Exception)
        {
            return StatusCode(500, $"An unexpected error occurred");
        }
    }
}
