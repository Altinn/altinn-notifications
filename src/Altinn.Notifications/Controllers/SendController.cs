using Altinn.Notifications.Core;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Provides endpoints for performing the actual sending of notifications.
    /// </summary>
    [ApiController]
    [Route("notifications/api/v1/send")]
    public class SendController : ControllerBase
    {
        private readonly INotifications _notificationsService;
        private readonly ILogger<SendController> _logger;

        /// <summary>
        /// Initializing a new instance of the <see cref="SendController"/> class.
        /// </summary>
        /// <param name="notificationService">The notification core service.</param>
        /// <param name="logger">A logger the controller can use to register log entries.</param>
        public SendController(INotifications notificationService, ILogger<SendController> logger)
        {
            _notificationsService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Trigger the sending of a specific notification target.
        /// </summary>
        /// <param name="value">The unique id of the target to send.</param>
        /// <returns>The result of the operation.</returns>
        [HttpPost]
        public async Task<ObjectResult> Post([FromBody] string value)
        {
            _logger.LogError($"// SendController // Post // Request to send target received");
            await _notificationsService.Send(Convert.ToInt32(value));
            return Ok("sendt");
        }
    }
}
