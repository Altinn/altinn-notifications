using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Extensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller for all operations related to SMS notification orders
/// </summary>
[Route("notifications/api/v1/orders/sms")]
[ApiController]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]

public class SmsNotificationOrdersController : ControllerBase
{
    private readonly IValidator<SmsNotificationOrderRequestExt> _validator;
    private readonly IOrderRequestService _orderRequestService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationOrdersController"/> class.
    /// </summary>
    public SmsNotificationOrdersController(IValidator<SmsNotificationOrderRequestExt> validator, IOrderRequestService orderRequestService)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
    }

    /// <summary>
    /// Send SMS notifications
    /// </summary>
    /// <remarks>
    /// Endpoint for sending SMS notifications to one or more recipients.
    /// </remarks>
    /// <returns>The notification order request response</returns>
    #pragma warning disable S1133
    [Obsolete("Legacy endpoint. Still supported, but going forward please use '/future/' endpoints instead.")]
    #pragma warning restore S1133
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The request was accepted and a notification order has been successfully generated.", typeof(NotificationOrderRequestResponseExt))]
    [SwaggerResponse(400, "The request was invalid.", typeof(ValidationProblemDetails))]
    [SwaggerResponse(401, "Indicates a missing, invalid or expired authorization header.")]
    [SwaggerResponse(403, "Indicates missing or invalid scope or Platform Access Token.")]
    [SwaggerResponseHeader(202, "Location", "string", "Link to access the newly created notification order.")]
    public async Task<ActionResult<NotificationOrderRequestResponseExt>> Post(SmsNotificationOrderRequestExt smsNotificationOrderRequest)
    {
        ValidationResult validationResult = _validator.Validate(smsNotificationOrderRequest);
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

        var orderRequest = smsNotificationOrderRequest.MapToOrderRequest(creator);
        NotificationOrderRequestResponse result = await _orderRequestService.RegisterNotificationOrder(orderRequest);

        return Accepted(result.OrderId.GetSelfLinkFromOrderId(), result.MapToExternal());
    }
}
