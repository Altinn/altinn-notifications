using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Integrations.Wolverine;

using Microsoft.AspNetCore.Mvc;

using Wolverine;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Temporary controller for testing Wolverine + Azure Service Bus end-to-end locally.
/// </summary>
[ApiController]
[Route("notifications/api/v1/test")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WolverineTestController : ControllerBase
{
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="WolverineTestController"/> class.
    /// </summary>
    public WolverineTestController(IMessageBus bus)
    {
        _bus = bus;
    }

    ///// <summary>
    ///// Publishes a test email delivery report command to the ASB queue.
    ///// </summary>
    //[HttpPost("email-delivery-report")]
    //public async Task<IActionResult> PublishEmailDeliveryReport(CancellationToken cancellationToken)
    //{
    //    var command = new EmailDeliveryReportCommand(
    //        NotificationId: Guid.NewGuid(),
    //        OperationId: $"test-op-{Guid.NewGuid():N}",
    //        SendResult: EmailNotificationResultType.Succeeded);

    //    await _bus.PublishAsync(command);
    //    return Ok(command);
    //}
}
