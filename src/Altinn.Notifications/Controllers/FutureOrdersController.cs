using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

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
    private readonly IDateTimeService _dateTimeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FutureOrdersController"/> class.
    /// </summary>
    public FutureOrdersController(
        IOrderRequestService orderRequestService,
        IDateTimeService dateTimeService)
    {
        _orderRequestService = orderRequestService;
        _dateTimeService = dateTimeService;
    }

    /// <summary>
    /// Creates a new notification order with zero or more reminders
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
    [SwaggerResponse(400, "The notification order is invalid", typeof(AltinnProblemDetails))]
    [SwaggerResponse(422, "Missing contact information for one or more recipients", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<NotificationOrderChainResponseExt>> Post(NotificationOrderChainRequestExt notificationOrderRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate using the new ValidationErrorBuilder pattern
            // This will throw a validation exception with proper error codes if validation fails
            NotificationOrderChainValidationHelper.ValidateOrderChainRequest(notificationOrderRequest, _dateTimeService.UtcNow());

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

            Result<NotificationOrderChainResponse> result = await _orderRequestService.RegisterNotificationOrderChain(notificationOrderChainRequest, cancellationToken);

            if (result.IsProblem)
            {
                var problemDetails = result.Problem!.ToProblemDetails();
                return StatusCode(problemDetails.Status!.Value, problemDetails);
            }

            return Created(result.Value!.OrderChainId.GetSelfLinkFromOrderChainId(), result.Value.MapToNotificationOrderChainResponseExt());
        }
        catch (ProblemInstanceException ex)
        {
            var problemDetails = ex.Problem.ToProblemDetails();
            problemDetails.Title = "One or more validation errors occurred.";  
            return StatusCode(problemDetails.Status ?? 400, problemDetails);
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
