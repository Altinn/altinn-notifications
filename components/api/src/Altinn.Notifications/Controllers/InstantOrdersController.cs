using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
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
/// Handles API requests for creating and processing instant notification orders.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
/// </remarks>
[ApiController]
[Route("notifications/api/v1/future/orders/instant")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController(
    IDateTimeService dateTimeService,
    IServiceOwnerService serviceOwnerService,
    IInstantOrderRequestService instantOrderRequestService,
    IValidator<InstantNotificationOrderRequestExt> validator,
    IValidator<InstantSmsNotificationOrderRequestExt> smsValidator,
    IValidator<InstantEmailNotificationOrderRequestExt> emailValidator) : ControllerBase
{
    /// <summary>
    /// Creates and sends an instant SMS notification to a single recipient (deprecated - use /sms endpoint instead).
    /// </summary>
    /// <param name="request">The request payload containing the details of the instant notification to be sent.</param>
    /// <param name="cancellationToken">A token used to monitor for cancellation requests, allowing the operation to be aborted if needed.</param>
    /// <returns>
    /// A response containing tracking information for the created notification order or an error response if the operation fails.
    /// </returns>
#pragma warning disable S1133
    [Obsolete("This endpoint is deprecated. Use the /instant/sms endpoint for SMS notifications instead.")]
#pragma warning restore S1133
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant notification was created.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing", typeof(AltinnProblemDetails))]
    public async Task<IActionResult> Post([FromBody] InstantNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request: request,
            idempotencyId: request.IdempotencyId,
            validator: validator,
            mapToOrder: (req, creator, timestamp) => req.MapToInstantNotificationOrder(creator, timestamp),
            persistOrder: instantOrderRequestService.PersistInstantSmsNotificationAsync,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates and sends an instant SMS notification to a single recipient.
    /// </summary>
    /// <param name="request">The request payload containing the details of the instant SMS notification to be sent.</param>
    /// <param name="cancellationToken">A token used to monitor for cancellation requests, allowing the operation to be aborted if needed.</param>
    /// <returns>
    /// A response containing tracking information for the created SMS notification order or an error response if the operation fails.
    /// </returns>
    [HttpPost("sms")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant SMS notification was created.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The SMS notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The SMS notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing", typeof(AltinnProblemDetails))]
    public async Task<IActionResult> PostSms([FromBody] InstantSmsNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request: request,
            idempotencyId: request.IdempotencyId,
            validator: smsValidator,
            mapToOrder: (req, creator, timestamp) => req.MapToInstantSmsNotificationOrder(creator, timestamp),
            persistOrder: instantOrderRequestService.PersistInstantSmsNotificationAsync,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates and sends an instant email notification to a single recipient.
    /// </summary>
    /// <param name="request">The request payload containing the details of the instant email notification to be sent.</param>
    /// <param name="cancellationToken">A token used to monitor for cancellation requests, allowing the operation to be aborted if needed.</param>
    /// <returns>
    /// A response containing tracking information for the created email notification order or an error response if the operation fails.
    /// </returns>
    [HttpPost("email")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(201, "The instant email notification was created.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(200, "The email notification order was created previously.", typeof(InstantNotificationOrderResponseExt))]
    [SwaggerResponse(400, "The email notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing", typeof(AltinnProblemDetails))]
    public async Task<IActionResult> PostEmail([FromBody] InstantEmailNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request: request,
            idempotencyId: request.IdempotencyId,
            validator: emailValidator,
            mapToOrder: (req, creator, timestamp) => req.MapToInstantEmailNotificationOrder(creator, timestamp),
            persistOrder: instantOrderRequestService.PersistInstantEmailNotificationAsync,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Processes an instant notification order with common workflow steps.
    /// </summary>
    private async Task<IActionResult> ProcessInstantOrderAsync<TRequest, TOrder>(
        TRequest request,
        string idempotencyId,
        IValidator<TRequest> validator,
        Func<TRequest, string, DateTime, TOrder> mapToOrder,
        Func<TOrder, CancellationToken, Task<InstantNotificationOrderTracking>> persistOrder,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        try
        {
            string? creator = HttpContext.GetOrg();

            // 1. Ensure the request is associated with a valid organization.
            if (string.IsNullOrWhiteSpace(creator))
            {
                return Forbid();
            }

            // 2. Validate the instant notification order, including that the senders are registered for the organization.
            var validationResult = validator.Validate(request, creator, serviceOwnerService);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            // 3. Check for existing order by organization short name and idempotency identifier.
            var trackingInformation = await instantOrderRequestService.RetrieveTrackingInformation(creator, idempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Map and persist the instant notification order.
            var instantOrder = mapToOrder(request, creator, dateTimeService.UtcNow());
            trackingInformation = await persistOrder(instantOrder, cancellationToken);

            // 5. Return tracking information and location header.
            return Created(trackingInformation.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
        catch (PlatformDependencyException)
        {
            var problemDetails = Problems.PlatformDependencyError.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
