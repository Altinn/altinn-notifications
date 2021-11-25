using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class OutboundController : ControllerBase
    {

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
        public IEnumerable<string> GetOutboundEmail()
        {
            List<string> result = new List<string>();
            result.Add("1");
            result.Add("2");
            result.Add("3");
            return result;
        }
    }
}
