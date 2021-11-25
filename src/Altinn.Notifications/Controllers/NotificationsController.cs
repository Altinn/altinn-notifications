using Altinn.Notifications.Interfaces.Models;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {

        /// <summary>
        /// Operation to 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<ValuesController>
        [HttpPost]
        public ObjectResult Post([FromBody] NotificationExt notification)
        {
            return Ok("Hurra");
        }
    }
}
