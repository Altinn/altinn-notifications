using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Sms.Controllers
{
    /// <summary>
    /// Controller for setup testing
    /// </summary>
    /// <param name="logger">Logger</param>
    [ApiController]
    [Route("[controller]")]
    public class SetupController(ILogger<SetupController> logger) : ControllerBase
    {
        private readonly ILogger<SetupController> _logger = logger;

        /// <summary>
        /// Get some string
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetSomeString")]
        public string GetSomeString()
        {
            return "SomeString";
        }
    }
}
