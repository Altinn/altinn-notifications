namespace Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel;


public class RecipientOrg: Recipient
{
    [JsonPropertyName("orgNumber")]
    public string OrgNumber { get; set; }
    
    
    [Description("The resource that the notification is related to, and that recipient elegebility will be evaluated on (e.g. when sending to an org. no will result in a notification to the official contact and only individuals with custom notifications AND access to the resource")]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }
    
    public ChannelScheme ChannelScheme { get; set; }
    
    public EmailSettings EmailSettings { get; set; }
    
    public SMSSettings SMSSettings { get; set; }
}
