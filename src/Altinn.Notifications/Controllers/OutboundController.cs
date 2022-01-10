using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving outbound notification targets.
    /// </summary>
    [ApiController]
    [Route("notifications/api/v1/outbound")]
    public class OutboundController : ControllerBase
    {
        private readonly INotifications _notificationsService;

        /// <summary>
        /// Initializing a new instance of the <see cref="OutboundController"/> class.
        /// </summary>
        /// <param name="notificationService">The notification core service.</param>
        public OutboundController(INotifications notificationService)
        {
            _notificationsService = notificationService;
        }

        /// <summary>
        /// Retrieve a list of outbound SMS notifications.
        /// </summary>
        /// <remarks>This is a dummy method currently not returning anything realted to actual notifications.</remarks>
        /// <returns>A list of SMS targets ready to be sent.</returns>
        [HttpGet("sms")]
        public IEnumerable<string> GetOutboundSms()
        {
            List<string> result = new List<string>();
            result.Add("1");
            result.Add("2");
            result.Add("3");
            return result;
        }

        /// <summary>
        /// Retrieve a list of outbound e-mail notifications.
        /// </summary>
        /// <returns>A list of e-mail targets ready to be sent.</returns>
        [HttpGet("email")]
        public async Task<IEnumerable<string>> GetOutboundEmail()
        {
            List<Target> targets = await _notificationsService.GetUnsentEmailTargets();

            List<string> unsentTargets = new List<string>();

            foreach (Target target in targets)
            {
                unsentTargets.Add(target.Id.ToString());
            }

            return unsentTargets;
        }
    }
}
