using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
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
[Route("notifications/api/v1/orders/instant")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController : ControllerBase
{
    private readonly string _defaultSmsSender;
    private readonly IOrderRequestService _orderRequestService;
    private readonly ISmsOrderProcessingService _smsOrderProcessingService;
    private readonly IShortMessageServiceClient _shortMessageServiceClient;
    private readonly IValidator<InstantNotificationOrderRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
    /// </summary>
    public InstantOrdersController(
        IOptions<NotificationConfig> config,
        IOrderRequestService orderRequestService,
        ISmsOrderProcessingService smsOrderProcessingService,
        IShortMessageServiceClient shortMessageServiceClient,
        IValidator<InstantNotificationOrderRequestExt> validator)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
        _smsOrderProcessingService = smsOrderProcessingService;
        _shortMessageServiceClient = shortMessageServiceClient;
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
    [SwaggerResponse(201, "The instant notification was created and sent.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    [SwaggerResponse(500, "An internal server error occurred while processing the notification order.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] InstantNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate the request.
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

            // 3. Check if an order with the same idempotency identifier already exists.
            var trackingInformation = await _orderRequestService.RetrieveInstantOrderTracking(creator, request.IdempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Register the instant notification order.
            var instantNotificationOrder = request.MapToInstantNotificationOrder(creator);
            var registerationResult = await _orderRequestService.RegisterInstantOrder(instantNotificationOrder, cancellationToken);
            if (registerationResult.IsError || registerationResult.Value == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Instant Notification Registration Failed",
                    Detail = "Failed to register the instant notification order."
                };
                return StatusCode(500, problemDetails);
            }

            // 5. Register the SMS notification order.
            var timeToLiveInSeconds = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds;
            await _smsOrderProcessingService.ProcessInstantOrder(registerationResult.Value, timeToLiveInSeconds, cancellationToken);

            // 6. Send out the SMS using the short message service client.
            var smsSendingResult = await _shortMessageServiceClient.SendAsync(instantNotificationOrder.MapToShortMessage(_defaultSmsSender), cancellationToken);
            if (!smsSendingResult.Success)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "SMS Sending Failed",
                    Detail = $"Failed to send the SMS."
                };
                return StatusCode(500, problemDetails);
            }

            return Created(instantNotificationOrder.OrderChainId.GetSelfLinkFromOrderChainId(), new InstantNotificationOrderResponseExt
            {
                OrderChainId = instantNotificationOrder.OrderChainId,
                Notification = new NotificationOrderChainShipmentExt
                {
                    ShipmentId = registerationResult.Value.Id,
                    SendersReference = registerationResult.Value.SendersReference
                }
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
                Status = 499,
                Title = "Request terminated",
                Detail = "The client disconnected or cancelled the request before the server could complete processing."
            };

            return StatusCode(499, problemDetails);
        }
    }
}
