using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WebApplication1
{
    public class NotificationOrderResponse
    {
        [Description("The uuid of the order")]
        [JsonPropertyName("notificationOrderId")]
        [JsonPropertyOrder(0)]
        public required Guid NotificationOrderId { get; set; }
        
        [Description("The notification contained in the order")]
        [JsonPropertyName("notification")]
        [JsonPropertyOrder(1)]
        public required NotificationResponse NotificationResponse { get; set; }
    }
}
