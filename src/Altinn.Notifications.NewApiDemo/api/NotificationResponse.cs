namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

public class NotificationResponse
{
    [Description("The uuid of the notification")]
    [JsonPropertyName("notificationId")]
    public required Guid NotificationId { get; set; }
    
    [Description("Type")]
    [JsonPropertyName("notificationType")]
    public required NotificationType NotificationType { get; set; }
    
    [Description("Reference determined bt the sender. Same as for the corresponding notification/reminder input")]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
    

}