using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.status
{
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentType>))]
    public enum ShipmentType
    {
        Notification,
        Reminder
    }
}
