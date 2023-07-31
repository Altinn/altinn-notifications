using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
    private readonly GeneralSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrdersController"/> class.
    /// </summary>
    public EmailNotificationOrdersController(IValidator<EmailNotificationOrderRequestExt> validator, IEmailNotificationOrderService orderService, IOptions<GeneralSettings> settings)
    {
        _validator = validator;
        _orderService = orderService;
        _settings = settings.Value;
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
        (NotificationOrder? registeredOrder, ServiceError? error) = await _orderService.RegisterEmailNotificationOrder(orderRequest);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        string selfLink = _settings.BaseUri + "/notifications/api/v1/orders/" + registeredOrder!.Id;
        return Accepted(selfLink, registeredOrder.Id);
    }
}