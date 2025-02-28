using System.ComponentModel;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api.status;

namespace WebApplication1
{
    public class ShipmentRecipient
    {
        [Description("The notification contained in the order")]
        [JsonPropertyName("type")]
        public ShipmentRecipientType Type { get; set; }
        
        [Description("The destination for this shipment, e.g. an email-address or phonenumber")]
        [JsonPropertyName("destination")]
        public string Destintion { get; set; }
        
        public string ShipmentRecipientStatus { get; set; }
    }
}
