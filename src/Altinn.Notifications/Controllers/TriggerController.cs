using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all trigger operations
/// </summary>
[Route("notifications/api/v1/trigger")]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class TriggerController : ControllerBase
{
    private readonly ILogger<TriggerController> _logger;
    private readonly IStatusFeedService _statusFeedService;
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
        INotificationScheduleService scheduleService,
        IOrderProcessingService orderProcessingService,
        ISmsNotificationService smsNotificationService,
        IEmailNotificationService emailNotificationService)
    {
        _logger = logger;
        _scheduleService = scheduleService;
        _statusFeedService = statusFeedService;
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
    /// Endpoint for starting the processing of emails that are ready to be sent
    /// </summary>
    [HttpPost]
    [Route("sendemail")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_SendEmailNotifications()
    {
        await _emailNotificationService.SendNotifications();
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
    /// Endpoint for starting the processing of sms that are ready to be sent with policy anytime
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    [HttpPost]
    [Route("sendsmsanytime")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_SendSmsNotificationsAnytime(CancellationToken cancellationToken = default)
    {
        await _smsNotificationService.SendNotifications(cancellationToken, SendingTimePolicy.Anytime);
        return Ok();
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
    /// Endpoint for starting the processing of sms that are ready to be sent with policy daytime
    /// Automatically filtered to process daytime sms only
    /// </summary>
    [HttpPost]
    [Route("sendsms")]
    [Route("sendsmsdaytime")]
    [Consumes("application/json")]
    public async Task<ActionResult> Trigger_SendSmsNotificationsDaytime(CancellationToken cancellationToken = default)
    {
        if (!_scheduleService.CanSendSmsNow())
        {
            return Ok();
        }

        await _smsNotificationService.SendNotifications(cancellationToken, SendingTimePolicy.Daytime);
        return Ok();
    }
}
