namespace WebApplication1;

using System.Text.Json.Serialization;

public class SMSSettings
{
    [JsonPropertyName("notificationScheme")]
    public required TransmissionScheme Scheme {get;set;}
    
    public string? message { get; set; }
}