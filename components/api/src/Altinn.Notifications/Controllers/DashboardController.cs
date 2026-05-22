using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for dashboard operations
/// </summary>
[ApiController]
[Route("notifications/api/v1/dashboard")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="dashboardService">The dashboard service.</param>
    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Retrieves all notifications for a recipient identified by their national identity number.
    /// </summary>
    /// <param name="nin">The national identity number of the recipient.</param>
    /// <param name="from">Start of the date range (inclusive). Defaults to 7 days ago if not provided.</param>
    /// <param name="to">End of the date range (exclusive). Defaults to now if not provided.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of notifications matching the search criteria.</returns>
    [HttpGet("notifications/nin")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved notifications", typeof(List<DashboardNotification>))]
    [SwaggerResponse(400, "Invalid request parameters")]
    public async Task<ActionResult<List<DashboardNotification>>> GetNotificationsByNin(
        [FromQuery] string nin,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nin))
        {
            return BadRequest("'nin' is required and cannot be empty");
        }

        if (from.HasValue && to.HasValue && from.Value >= to.Value)
        {
            return BadRequest("'from' must be earlier than 'to'.");
        }

        var result = await _dashboardService.GetNotificationsByNinAsync(nin, from, to, cancellationToken);
        return Ok(result);
    }
}
