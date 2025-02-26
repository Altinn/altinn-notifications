using System.ComponentModel;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api.shared;
using WebApplication1;

namespace Altinn.Notifications.NewApiDemo.api.status
{
    public class ShipmentStatus: BaseNotificationStatus
    {
        
        public string Status { get; set; }
        
        public string LastUpdated { get; set; }
        
        [Description("Type-identifier to determine if the shipment is a notification or a reminder")]
        [JsonPropertyName("shipmentType")]
        public ShipmentType ShipmentType { get; set; }
        
        [Description("List of recipients for the shipment. This attribute is not populated until the shipment is sent OK.")]
        [JsonPropertyName("recipients")]
        public List<ShipmentRecipient>? Recipients { get; set; }
    }
}
