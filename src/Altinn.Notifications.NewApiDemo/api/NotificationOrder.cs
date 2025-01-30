namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationOrder
{
    [JsonPropertyName("idempotencyId")] 
    [Description("sender-scoped unique key")]
    public required string IdempotencyId { get; set; }
    
    [JsonPropertyName("notifications")]
    [Description("List of notifications. A list may have only 1 instance of Notification.Type=Notifiction, and zero or more instances of Notification.Type=Reminder")]
    public List<Notification> Notifications { get; set; }
}