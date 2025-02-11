using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

public class RecipientSms: Recipient
{
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }
    
    //sms settings
    [JsonPropertyName("smsSettings")]
    public required SMSSettings SmsSettings { get; set; }
}
