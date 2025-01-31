using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WebApplication1
{
    public class BaseNotificationResponse
    {
        [Description("The uuid of the notification")]
        [JsonPropertyName("notificationId")]
        public required Guid NotificationId { get; set; }
    
        [Description("Reference determined bt the sender. Same as for the corresponding notification/reminder input")]
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }
    }
}
