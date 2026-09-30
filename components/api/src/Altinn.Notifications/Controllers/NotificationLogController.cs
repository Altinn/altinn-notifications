using System.Collections.Immutable;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.NotificationLog;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for retrieving notification log entries by Dialogporten identifiers.
/// </summary>
[ApiController]
[Route("notifications/api/v1/future")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
public class NotificationLogController(
    INotificationLogService notificationLogService,
    IDialogportenClient dialogportenClient) : ControllerBase
{
    private readonly INotificationLogService _notificationLogService = notificationLogService;
    private readonly IDialogportenClient _dialogportenClient = dialogportenClient;

    /// <summary>
    /// Retrieves notification log entries based on the provided Dialogporten identifiers.
    /// </summary>
    /// <param name="query">The Dialogporten identifiers to filter by. At least one must be provided.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A collection of matching notification log entries, or an empty list when no entries match.
    /// </returns>
    [HttpGet("log")]
    [Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
    [Produces("application/json")]
    [SwaggerResponse(200, "Notification log entries matching the provided identifiers were retrieved successfully", typeof(IImmutableList<NotificationLogSummaryExt>))]
    [SwaggerResponse(400, "One or more query parameters are invalid", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    [Obsolete("This endpoint is deprecated and deleted in a future release. No direct replacement is available.")]
    public async Task<ActionResult<ImmutableList<NotificationLogSummaryExt>>> Get(
        NotificationLogQueryExt query, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            string? creatorName = HttpContext.GetOrg();
            if (string.IsNullOrWhiteSpace(creatorName))
            {
                return Forbid();
            }

            return await GetLog(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }

    /// <summary>
    /// Gives end users access to their own notification log entries based on the provided
    /// Dialogporten identifiers.
    /// </summary>
    /// <remarks>End users identified with the help of ID-porten will be using this endpoint
    /// indirectly when looking at the activity log of a dialog in the portal. This is currently the
    /// only way to access this endpoint and the data it provides.</remarks>
    /// <param name="query">The Dialogporten identifiers to filter by. At least one must be provided.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A collection of matching notification log entries, or an empty list when no entries match.
    /// </returns>
    [HttpGet("enduser/log")]
    [Authorize(Policy = AuthorizationConstants.POLICY_END_USER_ACCESS)]
    [Produces("application/json")]
    [SwaggerResponse(200, "Notification log entries matching the provided identifiers were retrieved successfully", typeof(IImmutableList<NotificationLogSummaryExt>))]
    [SwaggerResponse(400, "One or more query parameters are invalid", typeof(AltinnProblemDetails))]
    [SwaggerResponse(499, "Request terminated - The client disconnected or cancelled the request", typeof(AltinnProblemDetails))]
    public async Task<ActionResult<ImmutableList<NotificationLogSummaryExt>>> GetForEnduser(
        NotificationLogQueryExt query, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            bool userHasAccess = await _dialogportenClient.CheckUserAccessToDialog(query.DialogId, cancellationToken);
            if (!userHasAccess)
            {
                return Forbid();
            }

            return await GetLog(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var problemDetails = Problems.RequestTerminated.ToProblemDetails();
            return StatusCode(problemDetails.Status!.Value, problemDetails);
        }
    }

    private async Task<ActionResult<ImmutableList<NotificationLogSummaryExt>>> GetLog(NotificationLogQueryExt query, CancellationToken cancellationToken)
    {
        IImmutableList<NotificationLogSummary> entries;

        if (query.TransmissionId.HasValue)
        {
            entries = await _notificationLogService.GetByDialogAndTransmissionIds(
                query.DialogId.ToString(), query.TransmissionId.Value.ToString(), cancellationToken);
        }
        else
        {
            entries = await _notificationLogService.GetByDialogId(query.DialogId.ToString(), cancellationToken);
        }

        return Ok(entries.MapToNotificationLogSummaryList());
    }
}
