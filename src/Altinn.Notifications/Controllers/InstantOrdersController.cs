using Altinn.Notifications.Configuration;
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
        try
        {
            // 1. Validate the instant notification order.
            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            // 2. Ensure the request is associated with a valid organization.
            var creator = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creator))
            {
                return Forbid();
            }

            // 3. Check for existing order by organization short name and idempotency identifier.
            var trackingInformation = await _instantOrderRequestService.RetrieveTrackingInformation(creator, request.IdempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Map and persist the instant notification order.
            var instantNotificationOrder = request.MapToInstantNotificationOrder(creator, _dateTimeService.UtcNow());
            trackingInformation = await _instantOrderRequestService.PersistInstantSmsNotificationAsync(instantNotificationOrder, cancellationToken);

            if (trackingInformation == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Instant notification order registration failed",
                    Detail = "An internal server error occurred while processing the notification order."
                };

                return StatusCode(500, problemDetails);
            }

            // 5. Return tracking information and location header.
            return Created(trackingInformation.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
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
        try
        {
            // 1. Validate the instant SMS notification order.
            var validationResult = _smsValidator.Validate(request);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            // 2. Ensure the request is associated with a valid organization.
            var creator = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creator))
            {
                return Forbid();
            }

            // 3. Check for existing order by organization short name and idempotency identifier.
            var trackingInformation = await _instantOrderRequestService.RetrieveTrackingInformation(creator, request.IdempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Map and persist the instant SMS notification order.
            var instantSmsNotificationOrder = request.MapToInstantSmsNotificationOrder(creator, _dateTimeService.UtcNow());
            trackingInformation = await _instantOrderRequestService.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder, cancellationToken);

            if (trackingInformation == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Instant SMS notification order registration failed",
                    Detail = "An internal server error occurred while processing the SMS notification order."
                };

                return StatusCode(500, problemDetails);
            }

            // 5. Return tracking information and location header.
            return Created(trackingInformation.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = 400,
                Detail = ex.Message,
                Title = "Invalid SMS notification order request"
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
        try
        {
            // 1. Validate the instant email notification order.
            var validationResult = _emailValidator.Validate(request);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            // 2. Ensure the request is associated with a valid organization.
            var creator = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creator))
            {
                return Forbid();
            }

            // 3. Check for existing order by organization short name and idempotency identifier.
            var trackingInformation = await _instantOrderRequestService.RetrieveTrackingInformation(creator, request.IdempotencyId, cancellationToken);
            if (trackingInformation != null)
            {
                return Ok(trackingInformation.MapToInstantNotificationOrderResponse());
            }

            // 4. Map and persist the instant email notification order.
            var instantEmailNotificationOrder = request.MapToInstantEmailNotificationOrder(creator, _dateTimeService.UtcNow());
            trackingInformation = await _instantOrderRequestService.PersistInstantEmailNotificationAsync(instantEmailNotificationOrder, cancellationToken);

            if (trackingInformation == null)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 500,
                    Title = "Instant email notification order registration failed",
                    Detail = "An internal server error occurred while processing the email notification order."
                };

                return StatusCode(500, problemDetails);
            }

            // 5. Return tracking information and location header.
            return Created(trackingInformation.OrderChainId.GetSelfLinkFromOrderChainId(), trackingInformation.MapToInstantNotificationOrderResponse());
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = 400,
                Detail = ex.Message,
                Title = "Invalid email notification order request"
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
