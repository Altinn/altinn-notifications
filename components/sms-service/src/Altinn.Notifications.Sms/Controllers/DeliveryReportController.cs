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
    private readonly ILogger<DeliveryReportController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryReportController"/> class.
    /// </summary>
    public DeliveryReportController(IStatusService statusService, ILogger<DeliveryReportController> logger)
    {
        _receiver = new(null, statusService.UpdateStatusAsync);
        _logger = logger;
    }

    /// <summary>
    /// Post method for handling delivery reports from Link Mobility
    /// </summary>
    [HttpPost]
    [Consumes("application/xml", "text/plain")]
    [SwaggerResponse(200, "The delivery report is received")]
    [SwaggerResponse(400, "The delivery report is invalid")]
    public async Task Post()
    {
        HttpContext.Request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(
                   HttpContext.Request.Body,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }

        HttpContext.Request.Body.Position = 0;
        _logger.LogError($"LinkMobilityPayload {body}");
        await _receiver.ReceiveDeliveryReportAsync(HttpContext);
    }
}
