using System.Text.Json;
using Altinn.Notifications.Email.Attributes;
using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Mappers;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;

using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Email.Controllers;

/// <summary>
/// Controller for handling delivery reports from Azure Communication Services
/// </summary>
[Route("notifications/email/api/v1/reports")]
[ApiController]
[AccessKey]
[SwaggerResponse(401, "Caller is unauthorized")]
public class DeliveryReportController : ControllerBase
{
    private readonly ILogger<DeliveryReportController> _logger;
    private readonly IStatusService _statusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryReportController"/> class.
    /// </summary>
    public DeliveryReportController(ILogger<DeliveryReportController> logger, IStatusService statusService)
    {
        _logger = logger;
        _statusService = statusService;
    }

    /// <summary>
    /// Post method for handling delivery reports from Azure Communication Services
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    [SwaggerResponse(200, "The delivery report is received")]
    [SwaggerResponse(400, "The delivery report is invalid")]
    public async Task<ActionResult<string>> Post([FromBody] EventGridEvent[] eventList)
    {
        foreach (EventGridEvent eventgridevent in eventList)
        {
            // If the event is a system event, TryGetSystemEventData will return the deserialized system event
            if (eventgridevent.TryGetSystemEventData(out object systemEvent))
            {
                switch (systemEvent)
                {
                    // To complete the validation handshake from Azure Event Grid, the subscriber must respond with validation code
                    case SubscriptionValidationEventData subscriptionValidated:
                        var responseData = new SubscriptionValidationResponse()
                        {
                            ValidationResponse = subscriptionValidated.ValidationCode
                        };
                        return JsonSerializer.Serialize(responseData);
                    case AcsEmailDeliveryReportReceivedEventData deliveryReport:
                        try 
                        { 
                            var operationResult = new SendOperationResult()
                            {
                                OperationId = deliveryReport.MessageId,
                                SendResult = EmailSendResultMapper.ParseDeliveryStatus(deliveryReport.Status?.ToString())
                            };
                            await _statusService.UpdateSendStatus(operationResult);    
                        } 
                        catch (ArgumentException ex)
                        {
                            _logger.LogError(
                                ex, 
                                "// DeliveryReportController // Post // Unknown deliverystatus (OperationId: '{OperationId}'). Delivery status: {Status}", 
                                deliveryReport.MessageId, 
                                deliveryReport.Status.ToString());
                            throw;
                        }

                        break;
                }
            }
        }

        return string.Empty;
    }
}
