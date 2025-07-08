using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to send instant notifications to one or more recipients.
/// </summary>
[ApiController]
[Route("notifications/api/v1/orders/instant")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController : ControllerBase
{
    private readonly IOrderRequestService _orderRequestService;
    private readonly INotificationsSmsClient _notificationsSmsClient;
    private readonly ISmsOrderProcessingService _smsOrderProcessingService;
    private readonly IValidator<InstantNotificationOrderRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
    /// </summary>
    public InstantOrdersController(
        IOrderRequestService orderRequestService,
        INotificationsSmsClient notificationsSmsClient,
        ISmsOrderProcessingService smsOrderProcessingService,
        IValidator<InstantNotificationOrderRequestExt> validator)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
        _notificationsSmsClient = notificationsSmsClient;
        _smsOrderProcessingService = smsOrderProcessingService;
    }

    /// <summary>
    /// Sends a notification instantly to one or more recipients.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after some basic validation of the request.
    /// </remarks>
    /// <param name="instantNotificationRequest">The instant notification order request</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
    /// <returns>Information about the created notification order</returns>
    [HttpPost()]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant notification was created and sent.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    public async Task<ActionResult<InstantNotificationOrderResponseExt>> Post([FromBody] InstantNotificationOrderRequestExt instantNotificationRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = _validator.Validate(instantNotificationRequest);
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

            var notificationOrderChainResponse = await _orderRequestService.RetrieveNotificationOrderChainTracking(creator, instantNotificationRequest.IdempotencyId, cancellationToken);
            if (notificationOrderChainResponse != null)
            {
                return Ok(notificationOrderChainResponse.MapToNotificationOrderChainResponseExt());
            }

            var notificationOrderChainRequest = instantNotificationRequest.MapToNotificationOrderChainRequest(creator);

            return StatusCode(501, new ProblemDetails
            {
                Status = 501,
                Title = "Instant notification order registration not implemented Yet",
                Detail = "This feature is currently under development and will be available soon. Please check back later."
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
