using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Controller for all operations related to sms notifications
    /// </summary>
    [Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
    [Route("notifications/api/v1/orders/{id}/notifications/sms")]
    [ApiController]
    [SwaggerResponse(401, "Caller is unauthorized")]
    [SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
    public class SmsNotificationsController : ControllerBase
    {
        private readonly ISmsNotificationSummaryService _summaryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationsController"/> class.
        /// </summary>
        /// <param name="summaryService">The notifications summary service</param>
        public SmsNotificationsController(ISmsNotificationSummaryService summaryService)
        {
            _summaryService = summaryService;
        }

        /// <summary>
        /// Endpoint for retrieving a summary of all sms notifications related to an order
        /// </summary>
        /// <param name="id">The order id</param>
        /// <returns>Sumarized order details and a list containing all sms notifications and their send status</returns>
        [HttpGet]
        [Produces("application/json")]
        [SwaggerResponse(200, "The notification order was accepted", typeof(SmsNotificationSummaryExt))]
        [SwaggerResponse(404, "No notification order mathching the id was found")]
        public async Task<ActionResult<SmsNotificationSummaryExt>> Get([FromRoute] Guid id)
        {
            string? expectedCreator = HttpContext.GetOrg();

            if (expectedCreator == null)
            {
                return Forbid();
            }

            Result<SmsNotificationSummary, ServiceError> result = await _summaryService.GetSmsSummary(id, expectedCreator);

            return result.Match(
                summary => Ok(summary.MapToSmsNotificationSummaryExt()),
                error => StatusCode(error.ErrorCode, error.ErrorMessage));
        }
    }
}
