using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to email notification orders
/// </summary>
[Route("notifications/api/v1/orders/email")]
[ApiController]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]

public class EmailNotificationOrdersController : ControllerBase
{
    private readonly IValidator<EmailNotificationOrderRequestExt> _validator;
    private readonly IOrderRequestService _orderRequestService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrdersController"/> class.
    /// </summary>
    public EmailNotificationOrdersController(IValidator<EmailNotificationOrderRequestExt> validator, IOrderRequestService orderRequestService)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
    }

    /// <summary>
    /// Send email notifications
    /// </summary>
    /// <remarks>
    /// Endpoint for sending an email notification to one or more recipient.
    /// </remarks>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The request was accepted and a notification order has been successfully generated.", typeof(NotificationOrderRequestResponseExt))]
    [SwaggerResponse(400, "The request was invalid.", typeof(ValidationProblemDetails))]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
    [SwaggerResponseHeader(202, "Location", "string", "Link to access the newly created notification order.")]
    public async Task<ActionResult<NotificationOrderRequestResponseExt>> Post(EmailNotificationOrderRequestExt emailNotificationOrderRequest)
    {
        var validationResult = _validator.Validate(emailNotificationOrderRequest);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(this.ModelState);
            return ValidationProblem(ModelState);
        }

        string? creator = HttpContext.GetOrg();

        if (creator == null)
        {
            return Forbid();
        }

        var orderRequest = emailNotificationOrderRequest.MapToOrderRequest(creator);
        NotificationOrderRequestResponse result = await _orderRequestService.RegisterNotificationOrder(orderRequest);

        return Accepted(result.OrderId!.GetSelfLinkFromOrderId(), result.MapToExternal());
    }
}
