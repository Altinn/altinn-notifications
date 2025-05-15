using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle future shipment endpoints
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/shipment")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class FutureShipmentController(IStatusFeedService statusFeedService) : ControllerBase
{
    /// <summary>
    /// Retrieve an array of order status change history.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet("feed")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult> GetStatusFeed(int seq = 0)
    {
        try
        {
            string? creatorName = HttpContext.GetOrg();
            if (creatorName == null)
            {
                return Forbid();
            }

            var result = await statusFeedService.GetStatusFeed(seq, creatorName);

            return Ok();
        }
        catch (TaskCanceledException e)
        {
            throw;
        }
    }
}
