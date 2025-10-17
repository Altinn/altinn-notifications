using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all trigger operations
/// </summary>
[ApiController]
[Route("notifications/api/v1/trigger")]
[ApiExplorerSettings(IgnoreApi = true)]
public class TriggerController : ControllerBase
{
    private readonly ILogger<TriggerController> _logger;
    private readonly IStatusFeedService _statusFeedService;
    private readonly ISmsPublishTaskQueue _smsPublishTaskQueue;
    private readonly IEmailPublishTaskQueue _emailPublishTaskQueue;
    private readonly INotificationScheduleService _scheduleService;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IEmailNotificationService _emailNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerController"/> class.
    /// </summary>
    public TriggerController(
        ILogger<TriggerController> logger,
        IStatusFeedService statusFeedService,
        ISmsPublishTaskQueue smsPublishTaskQueue,
        IEmailPublishTaskQueue emailPublishingTaskQueue,
        INotificationScheduleService scheduleService,
        IOrderProcessingService orderProcessingService,
        ISmsNotificationService smsNotificationService,
        IEmailNotificationService emailNotificationService)
    {
        _logger = logger;
        _scheduleService = scheduleService;
        _statusFeedService = statusFeedService;
        _smsPublishTaskQueue = smsPublishTaskQueue;
        _emailPublishTaskQueue = emailPublishingTaskQueue;
        _smsNotificationService = smsNotificationService;
        _orderProcessingService = orderProcessingService;
        _emailNotificationService = emailNotificationService;
    }

    /// <summary>
    /// Endpoint for starting the processing of past due orders
    /// </summary>
    [HttpPost]
    [Route("pastdueorders")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_PastDueOrders()
    {
        await _orderProcessingService.StartProcessingPastDueOrders();
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
            await _emailNotificationService.TerminateExpiredNotifications();
            await _smsNotificationService.TerminateExpiredNotifications();
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
            await _statusFeedService.DeleteOldStatusFeedRecords(cancellationToken);
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
