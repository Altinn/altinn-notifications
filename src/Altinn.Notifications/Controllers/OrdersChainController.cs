using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// Controller to handle notification orders that has one or more reminders.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("notifications/api/v1/orders/chain")]
public class OrdersChainController : ControllerBase
{
    private readonly IOrderRequestService _orderRequestService;
    private readonly IValidator<NotificationOrderChainRequestExt> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersChainController"/> class.
    /// </summary>
    public OrdersChainController(IOrderRequestService orderRequestService, IValidator<NotificationOrderChainRequestExt> validator)
    {
        _validator = validator;
        _orderRequestService = orderRequestService;
    }

    /// <summary>
    /// Creates a new notification order that has one or more reminders.
    /// </summary>
    /// <remarks>
    /// The API will accept the request after some basic validation of the request.
    /// The system will also attempt to verify that it will be possible to fulfill the order.
    /// </remarks>
    /// <param name="notificationOrderRequest">The notification order with reminders request</param>
    /// <returns>The notification order request response</returns>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerResponse(400, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(422, "The notification order is invalid", typeof(ValidationProblemDetails))]
    [SwaggerResponse(200, "The notification order was created.", typeof(NotificationOrderChainResponseExt))]
    [SwaggerResponse(201, "The notification order was created.", typeof(NotificationOrderChainResponseExt))]
    public async Task<ActionResult<NotificationOrderChainResponseExt>> Post(NotificationOrderChainRequestExt notificationOrderRequest)
    {
        var validationResult = _validator.Validate(notificationOrderRequest);
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

        var notificationOrderChainRequest = notificationOrderRequest.MapToNotificationOrderChainRequest(creator);
        NotificationOrderChainResponse result = await _orderRequestService.RegisterNotificationOrderChain(notificationOrderChainRequest);

        return Accepted(result.Id.GetSelfLinkFromOrderChainId(), result.MapToNotificationOrderChainResponseExt());
    }
}
