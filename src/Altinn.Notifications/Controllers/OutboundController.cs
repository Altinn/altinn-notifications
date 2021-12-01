using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class OutboundController : ControllerBase
    {
        private readonly INotifications _notificationsService;
        private readonly ILogger<OutboundController> _logger;

        public OutboundController(INotifications notificationService, ILogger<OutboundController> logger)
        {
            _notificationsService = notificationService;
            _logger = logger;
        }

        [HttpGet("sms")]
        public IEnumerable<string> GetOutboundSms()
        {
            List<string> result = new List<string>();
            result.Add("1");
            result.Add("2");
            result.Add("3");
            return result;
        }

        [HttpGet("email")]
        public async Task<IEnumerable<string>> GetOutboundEmail()
        {

            _logger.LogInformation($"// Outbound controller // GetOutboundEmail // Received a request");
           List<Target> targets = await  _notificationsService.GetUnsentEmailTargets();

            List<string> unsentTargets = new List<string>();

            foreach(Target target in targets)
            {
                unsentTargets.Add(target.Id.ToString());
            }

            return unsentTargets;
        }
    }
}
