namespace WebApplication1;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    Notification,
    Reminder
}
