using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Status;
using Altinn.Notifications.Validators;

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
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class StatusFeedController(IStatusFeedService statusFeedService) : ControllerBase
{
    /// <summary>
    /// Retrieve an array of order status change history.
    /// </summary>
    /// <param name="statusFeedRequest">The request object used to set optional variables for getting the status feed</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet("feed")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved status feed entries", typeof(List<StatusFeedExt>))]
    [SwaggerResponse(400, "The request is invalid", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<List<StatusFeedExt>>> GetStatusFeed([FromQuery] GetStatusFeedRequestExt statusFeedRequest)
    {
        try
        {
            // Validate using the new ValidationErrorBuilder pattern
            StatusFeedValidationHelper.ValidateStatusFeedRequest(statusFeedRequest);

            string? creatorName = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creatorName))
            {
                return Forbid();
            }

            var statusFeed = await statusFeedService.GetStatusFeed(statusFeedRequest.Seq, statusFeedRequest.PageSize, creatorName, HttpContext.RequestAborted);

            return Ok(statusFeed.MapToStatusFeedExtList());
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
