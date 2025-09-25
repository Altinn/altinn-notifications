using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Models.InstantEmail;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Email.Controllers;

/// <summary>
/// Controller for sending instant emails.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("notifications/email/api/v1/instantemail")]
public class InstantEmailController : ControllerBase
{
    private readonly ISendingService _sendingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantEmailController"/> class.
    /// </summary>
    public InstantEmailController(ISendingService sendingService)
    {
        _sendingService = sendingService;
    }

    /// <summary>
    /// Sends an email instantly to a single recipient.
    /// </summary>
    /// <param name="request">The request containing email content, content type, recipient, sender, subject, and notification ID.</param>
    /// <returns>
    /// Returns 202 (Accepted) when the email was successfully accepted for processing.
    /// Returns 400 (Bad Request) with <see cref="ProblemDetails"/> when the request is invalid or contains improper formatting.
    /// Returns 499 (Client Closed Request) with <see cref="ProblemDetails"/> when the client cancels the request before completion.
    /// </returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The email was accepted for processing.")]
    [SwaggerResponse(400, "The request was invalid.", typeof(ProblemDetails))]
    [SwaggerResponse(499, "The request was canceled before processing could complete.", typeof(ProblemDetails))]
    public async Task<IActionResult> Send([FromBody] InstantEmailRequest request)
    {
        var emailDataModel = MapToEmail(request);

        try
        {
            await _sendingService.SendAsync(emailDataModel);

            return Accepted();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid email request",
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
    /// Maps an <see cref="InstantEmailRequest"/> to the <see cref="Core.Sending.Email"/> domain model.
    /// </summary>
    /// <param name="request">The incoming request containing sender, subject, body, recipient, content type, and notification ID.</param>
    /// <returns>An <see cref="Core.Sending.Email"/> object populated with values from the request.</returns>
    private static Core.Sending.Email MapToEmail(InstantEmailRequest request)
    {
        return new Core.Sending.Email(
            notificationId: request.NotificationId,
            subject: request.Subject,
            body: request.Body,
            fromAddress: request.Sender,
            toAddress: request.Recipient,
            contentType: request.ContentType);
    }
}
