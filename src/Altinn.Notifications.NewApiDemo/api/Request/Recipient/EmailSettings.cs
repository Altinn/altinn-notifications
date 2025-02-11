namespace Altinn.Notifications.NewApiDemo.api.Recipient;

using System.Text.Json.Serialization;

public class EmailSettings
{
    [JsonPropertyName("notificationScheme")]
    public required TransmissionScheme Scheme {get;set;}
    
    public string Subject { get; set; }
    
    public string Body { get; set; }
}
