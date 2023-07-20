using System.Runtime.CompilerServices;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerController"/> class.
    /// </summary>
    public TriggerController(IOrderProcessingService orderProcessingService, IEmailNotificationService emailNotificationService)
    {
        _orderProcessingService = orderProcessingService;
        _emailNotificationService = emailNotificationService;
    }

    /// <summary>
    /// Endpoint for triggering the processing of past due orders
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
    /// Endpoint for retrieving email notifications that are ready to send
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("sendemail")]
    public async Task<ActionResult> Trigger_SendEmailNotifications()
    {
        await _emailNotificationService.SendNotifications();
        return Ok();
    }
}