using Altinn.Notifications.Core;

using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class SendController : ControllerBase
    {
        private readonly INotifications _notificationsService;
        private readonly ILogger<SendController> _logger;

        public SendController(INotifications notificationService, ILogger<SendController> logger)
        {
            _notificationsService = notificationService;
            _logger = logger;
        }

        // POST api/<SendController>
        [HttpPost]
        public async Task<ObjectResult> Post([FromBody] string value)
        {
            _logger.LogError($"// SendController // Post // Request to send target received");
            await _notificationsService.Send(Convert.ToInt32(value));
            return Ok("sendt");
        }
    }
}
