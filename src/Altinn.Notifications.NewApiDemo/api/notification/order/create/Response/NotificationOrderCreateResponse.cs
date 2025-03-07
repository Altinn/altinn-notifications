using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.notification.order.create.Response
{
    public class NotificationOrderCreateResponse
    {
        [Description("The uuid of the order")]
        [JsonPropertyName("notificationOrderId")]
        [JsonPropertyOrder(0)]
        public required Guid NotificationOrderId { get; set; }
        
        [Description("The notification contained in the order")]
        [JsonPropertyName("notification")]
        [JsonPropertyOrder(1)]
        public required NotificationOrderCreateShipmentResponseFragment NotificationOrderCreateShipmentResponseFragment { get; set; }
    }
}
