using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all trigger operations
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TriggerController"/> class.
/// </remarks>
[ApiController]
[Route("notifications/api/v1/trigger")]
[ApiExplorerSettings(IgnoreApi = true)]
public class TriggerController(
    ILogger<TriggerController> logger,
    IStatusFeedService statusFeedService,
    ISmsPublishTaskQueue smsPublishTaskQueue,
    INotificationScheduleService scheduleService,
    IOrderProcessingService orderProcessingService,
    IEmailPublishTaskQueue emailPublishingTaskQueue,
    IComposedEmailPublishSignal composedEmailPublishSignal,
    ITerminateExpiredNotificationsService terminateExpiredService) : ControllerBase
{
    private readonly ILogger<TriggerController> _logger = logger;
    private readonly IStatusFeedService _statusFeedService = statusFeedService;
    private readonly ISmsPublishTaskQueue _smsPublishTaskQueue = smsPublishTaskQueue;
    private readonly INotificationScheduleService _scheduleService = scheduleService;
    private readonly IOrderProcessingService _orderProcessingService = orderProcessingService;
    private readonly IEmailPublishTaskQueue _emailPublishTaskQueue = emailPublishingTaskQueue;
    private readonly IComposedEmailPublishSignal _composedEmailPublishSignal = composedEmailPublishSignal;
    private readonly ITerminateExpiredNotificationsService _terminateExpiredService = terminateExpiredService;

    /// <summary>
    /// Endpoint to trigger processing of one past due order. This is intended for testing and debugging purposes, and should not be used in production.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation that returns an <see cref="ActionResult"/>.</returns>
    [HttpPost]
    [Route("pastdueoneorder")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_PastDueOrder(CancellationToken cancellationToken = default)
    {
        await _orderProcessingService.TryProcessOrder(false, cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Endpoint to trigger processing for one retry order. This is intended for testing and debugging purposes, and should not be used in production
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation that returns an <see cref="ActionResult"/>.</returns>
    [HttpPost]
    [Route("retryoneorder")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_RetryOrder(CancellationToken cancellationToken = default)
    {
        await _orderProcessingService.TryProcessOrder(true, cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Signals background processing of email notifications.
    /// </summary>
    /// <returns>
    /// Always returns 200 OK, regardless of whether a new task was enqueued.
    /// </returns>
    [HttpPost]
    [Route("sendemail")]
    [Consumes("application/json")]
    public ActionResult Trigger_SendEmailNotifications()
    {
        _emailPublishTaskQueue.TryEnqueue();
        _composedEmailPublishSignal.TryEnqueue();
        return Ok();
    }

    /// <summary>
    /// Endpoint for terminating expired notifications
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Route("terminateexpirednotifications")]
    public async Task<IActionResult> Trigger_TerminateExpiredNotifications()
    {
        try
        {
            await _terminateExpiredService.TerminateExpiredNotifications();

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate expired notifications");
            return StatusCode(500, "Failed to terminate expired notifications");
        }
    }

    /// <summary>
    /// Endpoint for deleting old status feed records (older than 90 days)
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("deleteoldstatusfeedrecords")]
    public async Task<ActionResult> Trigger_DeleteOldStatusFeedRecords(CancellationToken cancellationToken = default)
    {
        try
        {
            var deletedCount = await _statusFeedService.DeleteOldStatusFeedRecords(cancellationToken);
            _logger.LogInformation("Deleted {DeletedCount} old status feed records", deletedCount);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old status feed records");
            return StatusCode(500, "Failed to delete old status feed records");
        }
    }

    /// <summary>
    /// Signals background processing of SMS notifications that use the <see cref="SendingTimePolicy.Anytime"/> policy.
    /// </summary>
    /// <returns>
    /// Always returns 200 OK. The response does not indicate whether a new task was actually queued.
    /// </returns>
    [HttpPost]
    [Route("sendsmsanytime")]
    [Consumes("application/json")]
    public ActionResult Trigger_SendSmsNotificationsAnytime()
    {
        _smsPublishTaskQueue.TryEnqueue(SendingTimePolicy.Anytime);
        return Ok();
    }

    /// <summary>
    /// Signals background processing of SMS notifications restricted to the <see cref="SendingTimePolicy.Daytime"/> window.
    /// </summary>
    /// <returns>
    /// Always returns 200 OK, regardless of whether processing was skipped (outside window) or a new task was enqueued.
    /// </returns>
    [HttpPost]
    [Route("sendsms")]
    [Route("sendsmsdaytime")]
    [Consumes("application/json")]
    public ActionResult Trigger_SendSmsNotificationsDaytime()
    {
        if (!_scheduleService.CanSendSmsNow())
        {
            return Ok();
        }

        _smsPublishTaskQueue.TryEnqueue(SendingTimePolicy.Daytime);
        return Ok();
    }
}
