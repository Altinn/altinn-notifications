using System.ComponentModel;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api.notification.order.status;
using Altinn.Notifications.NewApiDemo.api.shipment.status;

namespace Altinn.Notifications.NewApiDemo.api.shipment.feed
{
    public class ShipmentStatusFeedResponse
    {
        [Description("List of shipments in the feed, if any")]
        [JsonPropertyName("shipments")]
        [JsonPropertyOrder(1)]
        public required List<ShipmentStatusResponse> Shipments { get; set; }
    }
}
