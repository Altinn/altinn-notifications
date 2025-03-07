using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.notification.order.create.Response
{
    public class BaseNotificationCreateResponse
    {
        [Description("The uuid of the notification/reminder (collectively; shipment)")]
        [JsonPropertyName("shipmentId")]
        [JsonPropertyOrder(1)]
        public required Guid ShipmentId { get; set; }
    
        [Description("Reference determined bt the sender. Same as for the corresponding notification/reminder input")]
        [JsonPropertyName("sendersReference")]
        [JsonPropertyOrder(2)]
        public string? SendersReference { get; set; }
        

        
    }
}
