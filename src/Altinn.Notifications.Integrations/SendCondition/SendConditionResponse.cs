using System.Text.Json.Serialization;

namespace Altinn.Notifications.Integrations.SendCondition
{
    /// <summary>
    /// Condition response model
    /// </summary>
    public class SendConditionResponse
    {
        /// <summary>
        /// A boolean indicating whether a notification should be sent or not
        /// </summary>
        /// <remarks>
        /// Nullable to ensure default is not failse if deserialization fails
        /// </remarks>
        [JsonPropertyName("sendNotification")]
        public bool? SendNotification { get; set; }
    }
}
