using System.Collections.Immutable;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.NotificationLog;
using Altinn.Notifications.Validators.Extensions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for retrieving notification log entries by Dialogporten identifiers.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future/log")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class NotificationLogController(
    INotificationLogService notificationLogService,
    IValidator<NotificationLogQueryExt> validator) : ControllerBase
{
    private readonly IValidator<NotificationLogQueryExt> _validator = validator;
    private readonly INotificationLogService _notificationLogService = notificationLogService;

    /// <summary>
    /// Retrieves notification log entries filtered by dialog identifier, transmission identifier, or both.
    /// </summary>
    /// <param name="query">The Dialogporten identifiers to filter by. At least one must be provided.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A collection of matching notification log entries, or an empty list when no entries match.
    /// </returns>
    [HttpGet]
    [Produces("application/json")]
    [SwaggerResponse(200, "Notification log entries matching the provided identifiers were retrieved successfully", typeof(IImmutableList<NotificationLogSummaryExt>))]
    [SwaggerResponse(400, "One or more query parameters are invalid", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<ImmutableList<NotificationLogSummaryExt>>> Get([FromQuery] NotificationLogQueryExt query, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = _validator.Validate(query);
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

            IImmutableList<NotificationLogSummary> entries;

            bool hasDialogId = !string.IsNullOrWhiteSpace(query.DialogId);
            bool hasTransmissionId = !string.IsNullOrWhiteSpace(query.TransmissionId);
            if (hasDialogId && hasTransmissionId)
            {
                entries = await _notificationLogService.GetByDialogAndTransmissionIds(query.DialogId!, query.TransmissionId!, cancellationToken);
            }
            else if (hasDialogId)
            {
                entries = await _notificationLogService.GetByDialogId(query.DialogId!, cancellationToken);
            }
            else
            {
                entries = await _notificationLogService.GetByTransmissionId(query.TransmissionId!, cancellationToken);
            }

            return Ok(entries.MapToNotificationLogSummaryList());
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }
}
