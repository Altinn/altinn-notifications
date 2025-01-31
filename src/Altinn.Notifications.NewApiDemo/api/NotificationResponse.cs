namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationResponse : BaseNotificationResponse
{
    
    [JsonPropertyOrder(3)]
    public List<BaseNotificationResponse> Reminders { get; set; }

}
