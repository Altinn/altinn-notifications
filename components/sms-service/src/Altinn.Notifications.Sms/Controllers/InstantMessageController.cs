using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Models.InstantMessage;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Sms.Controllers;

/// <summary>
/// Controller for sending instant short messages.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("notifications/sms/api/v1/instantmessage/")]
public class InstantMessageController : ControllerBase
{
    private readonly ISendingService _sendingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantMessageController"/> class.
    /// </summary>
    public InstantMessageController(ISendingService sendingService)
    {
        _sendingService = sendingService;
    }

    /// <summary>
    /// Sends a short message instantly to a single recipient.
    /// </summary>
    /// <param name="request">The request containing message content, recipient, sender, notification ID, and time-to-live.</param>
    /// <returns>
    /// Returns 200 (OK) when the short message was successfully accepted by the service provider.
    /// Returns 400 (Bad Request) with <see cref="ProblemDetails"/> when the request is invalid or contains improper formatting.
    /// Returns 499 (Client Closed Request) with <see cref="ProblemDetails"/> when the client cancels the request before completion.
    /// </returns>
    [HttpPost("send")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(200, "The message was accepted by the service provider.")]
    [SwaggerResponse(400, "The request was invalid.", typeof(ProblemDetails))]
    [SwaggerResponse(499, "The request was canceled before processing could complete.", typeof(ProblemDetails))]
    public async Task<IActionResult> Send([FromBody] InstantMessageRequest request)
    {
        var smsDataModel = MapToSms(request);

        try
        {
            await _sendingService.SendAsync(smsDataModel, request.TimeToLive);

            return Ok();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid SMS request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The request could not be processed due to invalid input or state."
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new ProblemDetails
            {
                Title = "Request terminated",
                Status = StatusCodes.Status499ClientClosedRequest,
                Detail = "The request was canceled before processing could complete."
            });
        }
    }

    /// <summary>
    /// Maps an <see cref="InstantMessageRequest"/> to the <see cref="Core.Sending.Sms"/> domain model.
    /// </summary>
    /// <param name="request">The incoming request containing sender, message, recipient, notification ID, and time-to-live.</param>
    /// <returns>A <see cref="Core.Sending.Sms"/> object populated with values from the request.</returns>
    private static Core.Sending.Sms MapToSms(InstantMessageRequest request)
    {
        return new Core.Sending.Sms
        {
            Sender = request.Sender,
            Message = request.Message,
            Recipient = request.Recipient,
            NotificationId = request.NotificationId
        };
    }
}
