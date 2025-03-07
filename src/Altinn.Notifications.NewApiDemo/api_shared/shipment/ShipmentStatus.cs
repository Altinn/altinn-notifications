using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api_shared.shipment
{
    public class ShipmentStatus
    {
        
        
        [Description("The uuid of the notification")]
        [JsonPropertyName("shipmentId")]
        [JsonPropertyOrder(1)]
        public required Guid ShipmentId { get; set; }
    
        [Description("Reference determined by the sender. Same as for the corresponding notification/reminder input")]
        [JsonPropertyName("sendersReference")]
        [JsonPropertyOrder(2)]
        public string? SendersReference { get; set; }
        
        [Description("The current status of the shipment")]
        [JsonPropertyName("status")]
        [JsonPropertyOrder(3)]
        public required ShipmentStatusType Status { get; set; }
        
        [JsonPropertyOrder(4)]
        public required DateTime LastUpdated { get; set; }
        
        [Description("Type-identifier to determine if the shipment is a notification or a reminder")]
        [JsonPropertyName("shipmentType")]
        [JsonPropertyOrder(5)]
        public required ShipmentType ShipmentType { get; set; }
        
        [Description("List of recipients for the shipment. This attribute is not populated until the shipment is sent OK.")]
        [JsonPropertyName("recipients")]
        [JsonPropertyOrder(6)]
        public required List<ShipmentRecipient> Recipients { get; set; }

    }
}
