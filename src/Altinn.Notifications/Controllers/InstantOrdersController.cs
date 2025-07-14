using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Services.Interfaces;
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
/// This controller is responsible for receiving, processing, and responding to client requests concerning instant notifications.
/// </summary>
[ApiController]
[Route("notifications/api/v1/orders/instant")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController : ControllerBase
{
    private readonly IOrderRequestService _orderRequestService;
    private readonly ISmsOrderProcessingService _smsOrderProcessingService;
    private readonly IShortMessageServiceClient _shortMessageServiceClient;
    private readonly IValidator<InstantNotificationOrderRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
    /// </summary>
    public InstantOrdersController(
        IOrderRequestService orderRequestService,
        ISmsOrderProcessingService smsOrderProcessingService,
        IShortMessageServiceClient shortMessageServiceClient,
        IValidator<InstantNotificationOrderRequestExt> validator)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
        _smsOrderProcessingService = smsOrderProcessingService;
        _shortMessageServiceClient = shortMessageServiceClient;
    }

    /// <summary>
    /// Sends a notification instantly to one recipient.
    /// </summary>
    /// <remarks>
    /// This API endpoint accepts a request to send an instant notification after performing basic validation.
    /// </remarks>
    /// <param name="request">The request payload containing the details of the instant notification to be sent.</param>
    /// <param name="cancellationToken">A token used to monitor for cancellation requests, allowing the operation to be aborted if needed.</param>
    /// <returns>Returns tracking information to track the created notification order.</returns>
    [HttpPost()]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant notification was created and sent.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    public async Task<IActionResult> Post([FromBody] InstantNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        // Validate the request model
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        // Retrieve the creator's short name from the HTTP context.
        var creator = HttpContext.GetOrg();
        if (string.IsNullOrWhiteSpace(creator))
        {
            return Forbid();
        }

        try
        {
            // Check if the order already exists for the given creator and idempotency identifier.
            var orderChainTracking = await _orderRequestService.RetrieveInstantNotificationOrderTracking(creator, request.IdempotencyId, cancellationToken);
            if (orderChainTracking != null)
            {
                return Ok(orderChainTracking.MapToInstantNotificationOrderResponse());
            }

            // Create an instant notification order from the request.
            var instantNotificationOrder = request.MapToInstantNotificationOrder(creator);

            // Register the notification order.
            var notificationOrderCreationResult = await _orderRequestService.RegisterInstantNotificationOrder(instantNotificationOrder, cancellationToken);
            if (notificationOrderCreationResult.IsError || notificationOrderCreationResult.Value == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Notification order chain registration failed",
                    Detail = "Failed to register the instant notification order"
                };
                return StatusCode(500, problemDetails);
            }

            // Register the SMS notification.
            var expiryDateTime = notificationOrderCreationResult.Value.RequestedSendTime.AddSeconds(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds);
            await _smsOrderProcessingService.ProcessInstantOrder(notificationOrderCreationResult.Value, expiryDateTime, cancellationToken);

            // Send the SMS using the short message service client.
            var smsSendingResult = await _shortMessageServiceClient.SendAsync(instantNotificationOrder.MapToShortMessage(notificationOrderCreationResult.Value.Templates.OfType<SmsTemplate>().First().SenderNumber), cancellationToken);
            if (!smsSendingResult.Success)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "SMS sending failed",
                    Detail = "Failed to send the instant notification SMS"
                };
                return StatusCode(500, problemDetails);
            }

            return Created(instantNotificationOrder.OrderChainId.GetSelfLinkFromOrderChainId(), new InstantNotificationOrderResponseExt
            {
                OrderChainId = instantNotificationOrder.OrderChainId,
                Notification = new NotificationOrderChainShipmentExt
                {
                    ShipmentId = notificationOrderCreationResult.Value.Id,
                    SendersReference = notificationOrderCreationResult.Value.SendersReference
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
                Title = "Request terminated",
                Detail = "The client disconnected or cancelled the request before the server could complete processing.",
                Status = 499
            };

            return StatusCode(499, problemDetails);
        }
    }
}
