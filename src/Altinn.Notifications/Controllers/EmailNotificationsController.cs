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
    /// Controller for all operations related to email notifications
    /// </summary>
    [Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
    [Route("notifications/api/v1/orders/{id}/notifications/email")]
    [ApiController]
    [SwaggerResponse(401, "Caller is unauthorized")]
    [SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
    public class EmailNotificationsController : ControllerBase
    {
        private readonly IEmailNotificationSummaryService _summaryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailNotificationsController"/> class.
        /// </summary>
        /// <param name="summaryService">The notifications summary service</param>
        public EmailNotificationsController(IEmailNotificationSummaryService summaryService)
        {
            _summaryService = summaryService;
        }

        /// <summary>
        /// Endpoint for retrieving a summary of all email notifications related to an order
        /// </summary>
        /// <param name="id">The order id</param>
        /// <returns>Sumarized order details and a list containing all email notifications and their send status</returns>
        [HttpGet]
        [Produces("application/json")]
        [SwaggerResponse(200, "The notification order was accepted", typeof(EmailNotificationSummaryExt))]
        [SwaggerResponse(404, "No notification order mathching the id was found")]
        public async Task<ActionResult<EmailNotificationSummaryExt>> Get([FromRoute] Guid id)
        {
            string? expectedCreator = HttpContext.GetOrg();

            if (expectedCreator == null)
            {
                return Forbid();
            }

            Result<EmailNotificationSummary, ServiceError> result = await _summaryService.GetEmailSummary(id, expectedCreator);

            return result.Match(
                summary => Ok(summary.MapToEmailNotificationSummaryExt()),
                error => StatusCode(error.ErrorCode, error.ErrorMessage));
        }
    }
}
