using Altinn.Notifications.NewApiDemo.api.shared;

namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationStatus : BaseNotificationStatus
{
    
    [JsonPropertyOrder(3)]
    [JsonPropertyName("reminders")]
    public List<BaseNotificationStatus>? Reminders { get; set; }

}
