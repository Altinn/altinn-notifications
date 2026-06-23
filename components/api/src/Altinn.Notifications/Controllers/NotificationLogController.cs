using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for retrieving notification log entries.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/notification-log")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class NotificationLogController(INotificationLogRepository notificationLogRepository) : ControllerBase
{
    private readonly INotificationLogRepository _notificationLogRepository = notificationLogRepository;

    /// <summary>
    /// Retrieve notification log entries by ID.
    /// </summary>
    /// <param name="id">The ID to search for (shipment ID, dialog ID, or transmission ID)</param>
    /// <param name="idType">The type of ID being provided. Valid values: ShipmentId, DialogId, TransmissionId</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpGet]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(200, "Successfully retrieved notification log entries", typeof(List<object>))]
    [SwaggerResponse(400, "Invalid request parameters", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<IEnumerable<object>>> GetNotificationLogEntries(
        [FromQuery(Name = "id")] string id,
        [FromQuery(Name = "idType")] NotificationLogIdType idType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("The 'id' parameter is required and cannot be empty.");
            }

            if (!Enum.IsDefined(typeof(NotificationLogIdType), idType))
            {
                return BadRequest($"The 'idType' parameter must be one of: ShipmentId, DialogId, TransmissionId");
            }

            var entries = await _notificationLogRepository.GetNotificationLogEntries(id, idType, HttpContext.RequestAborted);

            return Ok(entries);
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
