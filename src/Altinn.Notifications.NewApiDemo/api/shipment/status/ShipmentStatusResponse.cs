using System.ComponentModel;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api_shared.shipment;
using Altinn.Notifications.NewApiDemo.api.notification.order.status;

namespace Altinn.Notifications.NewApiDemo.api.shipment.status
{
    public class ShipmentStatusResponse: ShipmentStatus
    {
        [Description("The global sequence-number for this particular shipment status")]
        [JsonPropertyName("sequenceNumber")]
        [JsonPropertyOrder(0)]
        public long SequenceNumber { get; set; }
        
        
    }
}
