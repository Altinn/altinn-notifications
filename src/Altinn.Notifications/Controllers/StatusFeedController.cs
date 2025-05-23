using System.ComponentModel.DataAnnotations;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;
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
public class StatusFeedController(IStatusFeedService statusFeedService, ILogger<StatusFeedController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieve an array of order status change history.
    /// </summary>
    /// <param name="seq">The sequence number to start fetching status feed entries from</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet("feed")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<List<StatusFeedExt>>> GetStatusFeed([FromQuery][Range(1, int.MaxValue)] int seq = 1)
    {
        try
        {
            string? creatorName = HttpContext.GetOrg();
            if (creatorName == null)
            {
                return Forbid();
            }

            var result = await statusFeedService.GetStatusFeed(seq, creatorName, HttpContext.RequestAborted);

            return result.Match<ActionResult>(
                statusFeed =>
                {
                    return Ok(statusFeed.MapToStatusFeedExtList(logger));
                },
                error =>
                {
                    return StatusCode(error.ErrorCode, new ProblemDetails
                    {
                        Status = error.ErrorCode,
                        Detail = error.ErrorMessage
                    });
                });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, "Request terminated - The client disconnected or cancelled the request before the server could complete processing");
        }
    }
}
