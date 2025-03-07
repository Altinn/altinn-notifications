using System.ComponentModel;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api_shared.shipment;

namespace Altinn.Notifications.NewApiDemo.api.notification.order.status
{
    public class NotificationOrderStatusResponse
    {
        [Description("The uuid of the order")]
        [JsonPropertyName("notificationOrderId")]
        [JsonPropertyOrder(1)]
        public required Guid NotificationOrderId { get; set; }

        [Description("The current status of the order")]
        [JsonPropertyName("status")]
        [JsonPropertyOrder(2)]
        public required string OrderStatus { get; set; }

        [Description("The notification/reminders contained in the order")]
        [JsonPropertyName("shipments")]
        [JsonPropertyOrder(3)]
        public required List<ShipmentStatus> ShipmentStatus { get; set; }
        
        
    }
}
