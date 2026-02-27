using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Controller for supporting automated testing of send condition processing
    /// </summary>
    [Route("notifications/api/v1/tests/sendcondition")]
    [Consumes("application/json")]
    [SwaggerTag("Private API")]
    [ExcludeFromCodeCoverage]
    public class SendConditionController : Controller
    {
        /// <summary>
        /// Condition endpoint that returns a send condition response.
        /// </summary>
        [HttpGet]
        public ActionResult Get([FromQuery] bool conditionMet)
        {
            return Ok(new SendConditionResponse() { SendNotification = conditionMet });
        }
    }

    /// <summary>
    /// Condition response model
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SendConditionResponse
    {
        /// <summary>
        /// Gets or sets a boolean indicating if the notification should be sent
        /// </summary>
        [JsonPropertyName("sendNotification")]
        public bool SendNotification { get; set; }
    }
}
