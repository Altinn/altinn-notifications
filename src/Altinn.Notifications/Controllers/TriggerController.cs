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
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly INotificationScheduleService _scheduleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerController"/> class.
    /// </summary>
    public TriggerController(
        IOrderProcessingService orderProcessingService,
        IEmailNotificationService emailNotificationService,
        ISmsNotificationService smsNotificationService,
        INotificationScheduleService scheduleService)
    {
        _orderProcessingService = orderProcessingService;
        _emailNotificationService = emailNotificationService;
        _smsNotificationService = smsNotificationService;
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// Endpoint for starting the processing of past due orders
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("pastdueorders")]
    public async Task<ActionResult> Trigger_PastDueOrders()
    {
        await _orderProcessingService.StartProcessingPastDueOrders();
        return Ok();
    }

    /// <summary>
    /// Endpoint for starting the processing of emails that are ready to be sent
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("sendemail")]
    public async Task<ActionResult> Trigger_SendEmailNotifications()
    {
        await _emailNotificationService.SendNotifications();
        return Ok();
    }

    /// <summary>
    /// Endpoint for starting the processing of sms that are ready to be sent
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("sendsms")]
    public async Task<ActionResult> Trigger_SendSmsNotifications()
    {
        if (!_scheduleService.CanSendSmsNotifications())
        {
            return Ok();
        }

        await _smsNotificationService.SendNotifications();
        return Ok();
    }
}
