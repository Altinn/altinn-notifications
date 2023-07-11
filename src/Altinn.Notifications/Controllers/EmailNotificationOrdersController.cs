using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to email notification orders
/// </summary>
[Route("notifications/api/v1/orders/email")]
[ApiController]
[Authorize]
public class EmailNotificationOrdersController : ControllerBase
{
    private readonly IValidator<EmailNotificationOrderRequest> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrdersController"/> class.
    /// </summary>
    public EmailNotificationOrdersController(IValidator<EmailNotificationOrderRequest> validator)
    {
        _validator = validator;
    }

    /// <summary>
    /// Add an order to create an email notification. 
    /// The API will accept the request after som basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </summary>
    /// <returns>The registered notification order</returns>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<NotificationOrderExt>> Post([FromBody] EmailNotificationOrderRequest emailNotificationOrderRequest)
    {
        var validationResult = _validator.Validate(emailNotificationOrderRequest);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(this.ModelState);
            return ValidationProblem(ModelState);
        }

        await Task.CompletedTask;

        return Accepted();
    }
}