using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class SendController : ControllerBase
    {
        // POST api/<SendController>
        [HttpPost]
        public ObjectResult Post([FromBody] string value)
        {
            return Ok("sendt");
        }
    }
}
