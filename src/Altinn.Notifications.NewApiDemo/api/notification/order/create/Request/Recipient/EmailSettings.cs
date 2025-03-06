using System.ComponentModel;

namespace Altinn.Notifications.NewApiDemo.api.Recipient;

using System.Text.Json.Serialization;

public class EmailSettings
{
    [JsonPropertyName("notificationScheme")]
    public required TransmissionScheme Scheme {get;set;}
    
    [JsonPropertyName("emailSender")]
    [Description("If set, this value is used as the sender email instead of noreply@altinn.no. The value used needs to be pre-configured to update DNS records to permit the Altinn mailserver to act on behalf of the sender domain.")] 
    public string? SenderAddress { get; set; }
    
    [JsonPropertyName("emailSenderName")]
    [Description("If set, this value is used as the sender email display name. Only applicable if emailSender is set and configured. It cannot be used on it's own")] 
    public string? SenderName { get; set; }
    
    
    public required string Subject { get; set; }
    
    public required string Body { get; set; }
}
