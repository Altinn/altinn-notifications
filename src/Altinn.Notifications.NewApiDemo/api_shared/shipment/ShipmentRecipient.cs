using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api_shared.shipment
{
    public class ShipmentRecipient
    {
        [Description("The notification contained in the order")]
        [JsonPropertyName("type")]
        [JsonPropertyOrder(1)]
        public ShipmentRecipientType Type { get; set; }
        
        [Description("The destination for this shipment, e.g. an email-address or phonenumber")]
        [JsonPropertyName("destination")]
        [JsonPropertyOrder(2)]
        public required string Destination { get; set; } //TODO: not following the strongly typed convention. Change this? 
        
        [Description("The current status of the shipment to this particular recipient")]
        [JsonPropertyName("status")]
        [JsonPropertyOrder(3)]
        public required ShipmentStatusType ShipmentRecipientStatus { get; set; }
    }
}
