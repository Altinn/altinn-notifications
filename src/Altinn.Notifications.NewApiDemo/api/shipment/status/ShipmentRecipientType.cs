using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.status
{
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentRecipientType>))]
    public enum ShipmentRecipientType
    {
        Email,
        SMS
    }
}
