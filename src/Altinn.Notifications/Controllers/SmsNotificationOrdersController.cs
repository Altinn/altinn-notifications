using System.Collections;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;

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
    /// Add an SMS notification order.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after som basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <returns>The id of the registered notification order</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(202, "The notification order was accepted", typeof(OrderIdExt))]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponseHeader(202, "Location", "string", "Link to access the newly created notification order.")]
    public async Task<ActionResult<OrderIdExt>> Post(SmsNotificationOrderRequestExt smsNotificationOrderRequest)
    {
        FluentValidation.Results.ValidationResult validationResult = _validator.Validate(smsNotificationOrderRequest);
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

        NotificationOrderRequest orderRequest = MapToOrderRequest(smsNotificationOrderRequest, creator);
        (NotificationOrder? registeredOrder, ServiceError? error) = await _orderRequestService.RegisterNotificationOrder(orderRequest);

        if (error != null)
        {
            return StatusCode(error.ErrorCode, error.ErrorMessage);
        }

        string selfLink = registeredOrder!.GetSelfLink();
        return Accepted(selfLink, new OrderIdExt(registeredOrder!.Id));
    }

    /// <summary>
    /// Maps a <see cref="SmsNotificationOrderRequestExt"/> to a <see cref="NotificationOrderRequest"/>
    /// </summary>
    public static NotificationOrderRequest MapToOrderRequest(SmsNotificationOrderRequestExt extRequest, string creator)
    {
        INotificationTemplate smsTemplate = new SmsTemplate(extRequest.SenderNumber, extRequest.Body);

        List<Recipient> recipients = new();

        recipients.AddRange(
            extRequest.Recipients.Select(r => new Recipient(string.Empty, new List<IAddressPoint>() { new SmsAddressPoint(r.MobileNumber!) })));

        return new NotificationOrderRequest(
            extRequest.SendersReference,
            creator,
            new List<INotificationTemplate>() { smsTemplate },
            extRequest.RequestedSendTime.ToUniversalTime(),
            NotificationChannel.Sms,
            recipients);
    }
}
