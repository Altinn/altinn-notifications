using System.Text.Json.Serialization;

namespace WebApplication1;

public class RecipientSMS: Recipient
{
    
    public string? PhoneNumber { get; set; }
    
    //sms settings
    [JsonPropertyName("smsSettings")]
    public required SMSSettings SmsSettings { get; set; }
}