using System.Text;

using Altinn.Notifications.Sms.Core.Status;

using LinkMobility.GatewayReceiver;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Sms.Controllers;

/// <summary>
/// Controller for handling delivery reports from Link Mobility
/// </summary>
[Authorize]
[Route("notifications/sms/api/v1/reports")]
[ApiController]
[SwaggerResponse(401, "Caller is unauthorized")]
public class DeliveryReportController : ControllerBase
{
    private readonly GatewayReceiver _receiver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryReportController"/> class.
    /// </summary>
    public DeliveryReportController(IStatusService statusService)
    {
        _receiver = new(null, statusService.UpdateStatusAsync);
    }

    /// <summary>
    /// Post method for handling delivery reports from Link Mobility
    /// </summary>
    [HttpPost]
    [Consumes("application/xml", "text/plain")]
    [SwaggerResponse(200, "The delivery report is received")]
    [SwaggerResponse(400, "The delivery report is invalid")]
    public async Task<ActionResult<string>> Post()
    {
        var res = await _receiver.ReceiveDeliveryReportAsync(HttpContext);
        return StatusCode((int)res.status, res.responseBody);
    }
}
