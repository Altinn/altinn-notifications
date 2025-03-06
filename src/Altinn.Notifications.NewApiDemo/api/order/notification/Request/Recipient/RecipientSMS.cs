using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.Recipient;

public class RecipientSms
{
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }
    
    //sms settings
    [JsonPropertyName("smsSettings")]
    [Required] //This is in place instead of a non-nullable required (as EmailAddress), to avoid OpenAPI generating a non-nullable EmailSettings2 contract...
    public SMSSettings? SmsSettings { get; set; }
}
