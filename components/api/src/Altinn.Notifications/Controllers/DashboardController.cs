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
    /// <returns>A list of notifications matching the search criteria.</returns>
    [HttpGet("notifications/nin")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved notifications", typeof(List<DashboardNotification>))]
    public async Task<ActionResult<List<DashboardNotification>>> GetNotificationsByNin(
        [FromQuery] string nin,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await _dashboardService.GetNotificationsByNinAsync(nin, from, to);
        return Ok(result);
    }
}
