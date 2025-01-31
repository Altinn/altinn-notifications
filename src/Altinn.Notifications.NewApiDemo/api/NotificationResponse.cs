namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationResponse : BaseNotificationResponse
{
    
    
    public List<BaseNotificationResponse> Reminders { get; set; }

}
