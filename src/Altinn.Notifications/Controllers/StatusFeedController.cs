using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Status;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for managing notification status feeds.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/shipment")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[SwaggerResponse(499, "The operation was cancelled by the caller")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class StatusFeedController(IStatusFeedService statusFeedService, IValidator<GetStatusFeedRequestExt> validator) : ControllerBase
{
    private readonly IValidator<GetStatusFeedRequestExt> _validator = validator;

    /// <summary>
    /// Retrieve an array of order status change history.
    /// </summary>
    /// <param name="statusFeedRequest">The request object used to set optional variables for getting the status feed</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet("feed")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved status feed entries", typeof(List<StatusFeedExt>))]
    public async Task<ActionResult<List<StatusFeedExt>>> GetStatusFeed([FromQuery] GetStatusFeedRequestExt statusFeedRequest)
    {
        try
        {
            var validationResult = _validator.Validate(statusFeedRequest);
            if (!validationResult.IsValid)
            {
                validationResult.AddToModelState(ModelState);
                return ValidationProblem(ModelState);
            }

            string? creatorName = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creatorName))
            {
                return Forbid();
            }

            var result = await statusFeedService.GetStatusFeed(statusFeedRequest.Seq, statusFeedRequest.PageSize, creatorName, HttpContext.RequestAborted);

            return result.Match<ActionResult>(
                statusFeed =>
                {
                    return Ok(statusFeed.MapToStatusFeedExtList());
                },
                error =>
                {
                    return StatusCode(error.ErrorCode, new ProblemDetails
                    {
                        Type = error.ErrorType,
                        Title = "Failed to retrieve status feed",
                        Status = error.ErrorCode,
                        Detail = error.ErrorMessage
                    });
                });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new ProblemDetails
            {
                Type = "request-terminated",
                Title = "Request terminated",
                Detail = "The client disconnected or cancelled the request before the server could complete processing.",
                Status = 499
            });
        }
    }
}
