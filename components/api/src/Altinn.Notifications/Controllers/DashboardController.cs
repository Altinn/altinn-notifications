using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;

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

[Authorize(Policy = AuthorizationConstants.POLICY_SUPPORT_DASHBOARD_ACCESS)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IValidator<GetNotificationsByNinRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="dashboardService">The dashboard service.</param>
    /// <param name="validator">The validator for NIN lookup requests.</param>
    public DashboardController(IDashboardService dashboardService, IValidator<GetNotificationsByNinRequestExt> validator)
    {
        _dashboardService = dashboardService;
        _validator = validator;
    }

    /// <summary>
    /// Retrieves all notifications for a recipient identified by their national identity number.
    /// </summary>
    /// <param name="request">The request containing the NIN and optional date range.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of notifications matching the search criteria.</returns>
    [HttpGet("notifications/nin")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved notifications", typeof(List<DashboardNotificationExt>))]
    [SwaggerResponse(400, "Invalid request parameters")]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<List<DashboardNotificationExt>>> GetNotificationsByNin(
        [FromQuery] GetNotificationsByNinRequestExt request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _dashboardService.GetNotificationsByNinAsync(request.Nin, request.From, request.To, cancellationToken);
            return Ok(result.MapToDashboardNotificationExtList());
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
