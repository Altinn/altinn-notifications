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

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerController"/> class.
    /// </summary>
    public TriggerController(IOrderProcessingService orderProcessingService)
    {
        _orderProcessingService = orderProcessingService;
    }

    /// <summary>
    /// Endpoint for triggering the processing of past due orders
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("pastdueorders")]    
    public async Task<ActionResult> Trigger_PastDueOrders()
    {
        await _orderProcessingService.ProcessPastDueOrders();
        return Ok();
    }

    /// <summary>
    /// Endpoint for triggering the processing of peding orders
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Route("pendingorders")]
    public async Task<ActionResult> Trigger_PendingOrders()
    {
        await _orderProcessingService.ProcessPendingOrders();
        return Ok();
    }
}