using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api_shared.shipment
{
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentType>))]
    public enum ShipmentType
    {
        Notification,
        Reminder
    }
}
