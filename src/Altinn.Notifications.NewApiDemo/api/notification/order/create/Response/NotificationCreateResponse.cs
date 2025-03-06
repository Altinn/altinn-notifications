using Altinn.Notifications.NewApiDemo.api.shared;

namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationCreateResponse : BaseNotificationCreateResponse
{
    
    [JsonPropertyOrder(3)]
    [JsonPropertyName("reminders")]
    public List<BaseNotificationCreateResponse>? Reminders { get; set; }

}
