using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.notification.order.create.Response;

public class NotificationOrderCreateShipmentResponseFragment : BaseNotificationCreateResponse
{
    
    [JsonPropertyOrder(3)]
    [JsonPropertyName("reminders")]
    public List<BaseNotificationCreateResponse>? Reminders { get; set; }

}
