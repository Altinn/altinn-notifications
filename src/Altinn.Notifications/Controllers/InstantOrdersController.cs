using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
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
[ApiController]
[Route("notifications/api/v1/future/orders/instant")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class InstantOrdersController : ControllerBase
{
    private readonly IDateTimeService _dateTimeService;
    private readonly IInstantOrderRequestService _instantOrderRequestService;
    private readonly IValidator<InstantNotificationOrderRequestExt> _validator;
    private readonly IValidator<InstantSmsNotificationOrderRequestExt> _smsValidator;
    private readonly IValidator<InstantEmailNotificationOrderRequestExt> _emailValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrdersController"/> class.
    /// </summary>
    public InstantOrdersController(
        IDateTimeService dateTimeService,
        IInstantOrderRequestService instantOrderRequestService,
        IValidator<InstantNotificationOrderRequestExt> validator,
        IValidator<InstantSmsNotificationOrderRequestExt> smsValidator,
        IValidator<InstantEmailNotificationOrderRequestExt> emailValidator)
    {
        _validator = validator;
        _smsValidator = smsValidator;
        _emailValidator = emailValidator;
        _dateTimeService = dateTimeService;
        _instantOrderRequestService = instantOrderRequestService;
    }

    /// <summary>
    /// Creates and sends an instant SMS notification to a single recipient (deprecated - use /sms endpoint instead).
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
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    [SwaggerResponse(500, "An internal server error occurred while processing the notification order")]
    [Obsolete("This endpoint is deprecated. Use the /instant/sms endpoint for SMS notifications instead.")]
    public async Task<IActionResult> Post([FromBody] InstantNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request,
            _validator,
            (req, creator, timestamp) => req.MapToInstantNotificationOrder(creator, timestamp),
            _instantOrderRequestService.PersistInstantSmsNotificationAsync,
            "notification",
            cancellationToken);
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
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    [SwaggerResponse(500, "An internal server error occurred while processing the SMS notification order")]
    public async Task<IActionResult> PostSms([FromBody] InstantSmsNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request,
            _smsValidator,
            (req, creator, timestamp) => req.MapToInstantSmsNotificationOrder(creator, timestamp),
            _instantOrderRequestService.PersistInstantSmsNotificationAsync,
            "SMS",
            cancellationToken);
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
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing")]
    [SwaggerResponse(500, "An internal server error occurred while processing the email notification order")]
    public async Task<IActionResult> PostEmail([FromBody] InstantEmailNotificationOrderRequestExt request, CancellationToken cancellationToken = default)
    {
        return await ProcessInstantOrderAsync(
            request,
            _emailValidator,
            (req, creator, timestamp) => req.MapToInstantEmailNotificationOrder(creator, timestamp),
            _instantOrderRequestService.PersistInstantEmailNotificationAsync,
            "email",
            cancellationToken);
    }

    /// <summary>
    /// Processes an instant notification order with common workflow steps.
    /// </summary>
    private async Task<IActionResult> ProcessInstantOrderAsync<TRequest, TOrder>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<TRequest, string, DateTime, TOrder> mapToOrder,
        Func<TOrder, CancellationToken, Task<InstantNotificationOrderTracking?>> persistOrder,
        string orderType,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        try
        {
            // 1. Validate the instant notification order.
            var validationError = ValidateRequest(validator, request);
            if (validationError != null)
            {
                return validationError;
            }

            // 2. Ensure the request is associated with a valid organization.
            var creatorError = GetCreatorOrForbid(out string creator);
            if (creatorError != null)
            {
                return creatorError;
            }

            // 3. Check for existing order by organization short name and idempotency identifier.
            var idempotencyId = GetIdempotencyId(request);
            var trackingInformation = await _instantOrderRequestService.RetrieveTrackingInformation(creator, idempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Map and persist the instant notification order.
            var instantOrder = mapToOrder(request, creator, _dateTimeService.UtcNow());
            trackingInformation = await persistOrder(instantOrder, cancellationToken);

            if (trackingInformation == null)
            {
                return StatusCode(500, CreateProblemDetails(
                    500,
                    $"Instant {orderType} notification order registration failed",
                    $"An internal server error occurred while processing the {orderType} notification order."));
            }

            // 5. Return tracking information and location header.
            return Created(trackingInformation.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
        }
        catch (Exception ex)
        {
            return HandleCommonExceptions(ex);
        }
    }

    /// <summary>
    /// Extracts the idempotency ID from a request object.
    /// </summary>
    private static string GetIdempotencyId<T>(T request)
    {
        return request switch
        {
            InstantNotificationOrderRequestExt notificationRequest => notificationRequest.IdempotencyId,
            InstantSmsNotificationOrderRequestExt smsRequest => smsRequest.IdempotencyId,
            InstantEmailNotificationOrderRequestExt emailRequest => emailRequest.IdempotencyId,
            _ => throw new ArgumentException($"Unsupported request type: {typeof(T)}")
        };
    }

    /// <summary>
    /// Validates a request and returns validation problem if invalid.
    /// </summary>
    private IActionResult? ValidateRequest<T>(IValidator<T> validator, T request)
    {
        var validationResult = validator.Validate(request);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        return null;
    }

    /// <summary>
    /// Gets organization from HTTP context and returns Forbid if invalid.
    /// </summary>
    private IActionResult? GetCreatorOrForbid(out string creator)
    {
        creator = HttpContext.GetOrg() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(creator))
        {
            return Forbid();
        }

        return null;
    }

    /// <summary>
    /// Creates appropriate problem details for different error scenarios.
    /// </summary>
    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
    }

    /// <summary>
    /// Handles common exceptions and returns appropriate error responses.
    /// </summary>
    private IActionResult HandleCommonExceptions(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException => StatusCode(400, CreateProblemDetails(
                400,
                "Invalid notification order request",
                ex.Message)),
            OperationCanceledException => StatusCode(499, CreateProblemDetails(
                499,
                "Request terminated",
                "The client disconnected or cancelled the request before the server could complete processing.")),
            _ => throw ex
        };
    }
}
