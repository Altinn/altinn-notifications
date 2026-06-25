using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Models.Orders;
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
/// Controller for submitting composed email notification orders.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/orders/email-with-attachments")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_EMAIL_WITH_ATTACHMENTS_CREATE_SCOPE)]
public class NotificationOrderWithAttachmentsController : ControllerBase
{
    private readonly IOrderRequestService _orderRequestService;
    private readonly IValidator<ComposedEmailRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderWithAttachmentsController"/> class.
    /// </summary>
    public NotificationOrderWithAttachmentsController(
        IOrderRequestService orderRequestService,
        IValidator<ComposedEmailRequestExt> validator)
    {
        _orderRequestService = orderRequestService;
        _validator = validator;
    }

    /// <summary>
    /// Creates a new composed email notification order.
    /// </summary>
    /// <remarks>
    /// The API validates the request at the boundary before any database write or queue publish.
    /// Files are referenced by SAS URL and downloaded by the email service at send time.
    /// </remarks>
    /// <param name="orderRequest">The composed email order request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The notification order chain response.</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The notification order was created.", typeof(NotificationOrderChainResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(NotificationOrderChainResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid.", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "Missing contact information for one or more recipients", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<NotificationOrderChainResponseExt>> Post(ComposedEmailRequestExt orderRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = _validator.Validate(orderRequest);
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

            var orderChainTracking = await _orderRequestService.RetrieveOrderChainTracking(creator, orderRequest.IdempotencyId, cancellationToken);
            if (orderChainTracking != null)
            {
                return Ok(orderChainTracking.MapToNotificationOrderChainResponseExt());
            }

            var notificationOrderChainRequest = orderRequest.MapToNotificationOrderChainRequest(creator);

            Result<NotificationOrderChainResponse> result = await _orderRequestService.RegisterNotificationOrderChain(notificationOrderChainRequest, cancellationToken);

            if (result.IsProblem)
            {
                var problemDetails = result.Problem!.ToProblemDetails();
                return StatusCode(problemDetails.Status!.Value, problemDetails);
            }

            return Created(result.Value!.OrderChainId.GetSelfLinkFromOrderChainId(), result.Value.MapToNotificationOrderChainResponseExt());
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
