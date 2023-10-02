using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
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
    private readonly IValidator<EmailNotificationOrderRequestExt> _validator;
    private readonly IEmailNotificationOrderService _orderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrdersController"/> class.
    /// </summary>
    public EmailNotificationOrdersController(IValidator<EmailNotificationOrderRequestExt> validator, IEmailNotificationOrderService orderService)
    {
        _validator = validator;
        _orderService = orderService;
    }

    /// <summary>
    /// Add an order to create an email notification. 
    /// The API will accept the request after som basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </summary>
    /// <returns>The registered notification order</returns>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<NotificationOrderExt>> Post(EmailNotificationOrderRequestExt emailNotificationOrderRequest)
    {
        var validationResult = _validator.Validate(emailNotificationOrderRequest);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(this.ModelState);
            return ValidationProblem(ModelState);
        }

        string? creator = User.GetOrg();

        if (creator == null)
        {
            return Forbid();
        }

        var orderRequest = emailNotificationOrderRequest.MapToOrderRequest(creator);
        var result = await _orderService.RegisterEmailNotificationOrder(orderRequest);

        return result.Match(
            successValue =>
            {
                string selfLink = successValue.GetSelfLink();
                return Accepted(selfLink, new OrderIdExt(successValue.Id));
            },
            errorValue => ValidationProblem(errorValue.ErrorMessage, statusCode: errorValue.ErrorCode));
    }
}
