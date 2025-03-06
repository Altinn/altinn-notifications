namespace Altinn.Notifications.NewApiDemo.api.Recipient;

using System.Text.Json.Serialization;

public class SMSSettings
{
    [JsonPropertyName("notificationScheme")]
    public required TransmissionScheme Scheme {get;set;}
    
    public string? Sender { get; set; }
    
    public string message { get; set; }
}
