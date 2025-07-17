using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Handles API requests for creating and processing instant notification orders.
/// </summary>
[ApiController]
[SwaggerResponse(401, "Caller is unauthorized")]
[Route("notifications/api/v1/future/orders/instant")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController : ControllerBase
{
    private readonly string _defaultSmsSender;
    private readonly IDateTimeService _dateTimeService;
    private readonly IShortMessageServiceClient _shortMessageServiceClient;
    private readonly IInstantOrderRequestService _instantOrderRequestService;
    private readonly IValidator<InstantNotificationOrderRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
    /// </summary>
    public InstantOrdersController(
        IDateTimeService dateTimeService,
        IOptions<NotificationConfig> config,
        IShortMessageServiceClient shortMessageServiceClient,
        IInstantOrderRequestService instantOrderRequestService,
        IValidator<InstantNotificationOrderRequestExt> validator)
    {
        _validator = validator;
        _dateTimeService = dateTimeService;
        _shortMessageServiceClient = shortMessageServiceClient;
        _instantOrderRequestService = instantOrderRequestService;
        _defaultSmsSender = config.Value.DefaultSmsSenderNumber;
    }

    /// <summary>
    /// Creates and sends an instant notification to a single recipient.
    /// </summary>
    /// <param name="request">The request payload containing the details of the instant notification to be sent.</param>
    /// <param name="cancellationToken">A token used to monitor for cancellation requests, allowing the operation to be aborted if needed.</param>
    /// <returns>
    /// A response containing tracking information for the created notification order or an error response if the operation fails.
    /// </returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant notification was created.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    [SwaggerResponse(500, "An internal server error occurred while processing the notification order.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] InstantNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate the instant notification order.
            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            // 2. Retrieve the creator's short name.
            var creator = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creator))
            {
                return Forbid();
            }

            // 3. Return the tracking information if the idempotency identifier already exists.
            var trackingInformation = await _instantOrderRequestService.RetrieveTrackingInformation(creator, request.IdempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Persist the instant notification order into the databaase.
            var instantNotificationOrder = request.MapToInstantNotificationOrder(creator, _dateTimeService.UtcNow());
            trackingInformation = await _instantOrderRequestService.PersistInstantSmsNotificationAsync(instantNotificationOrder, cancellationToken);
            if (trackingInformation == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Registration failed",
                    Detail = "Failed to register the instant notification order."
                };

                return StatusCode(500, problemDetails);
            }

            // 5. Send the SMS using the short message service client.
            _ = Task.Run(async () => { await _shortMessageServiceClient.SendAsync(instantNotificationOrder.MapToShortMessage(_defaultSmsSender)); }, CancellationToken.None);

            // 6. Return the tracking information.
            return Created(instantNotificationOrder.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
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
                Status = 499,
                Title = "Request terminated",
                Detail = "The client disconnected or cancelled the request before the server could complete processing."
            };

            return StatusCode(499, problemDetails);
        }
    }
}
