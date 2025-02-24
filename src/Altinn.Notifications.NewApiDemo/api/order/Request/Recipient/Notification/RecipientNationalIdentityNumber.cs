using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

public class RecipientNationalIdentityNumber: Recipient
{
    
    public string? NationalIdentityNumber { get; set; }
    
    [Description("The resource that the notification is related to, and that recipient elegebility will be evaluated on (e.g. when sending to an org. no will result in a notification to the official contact and only individuals with custom notifications AND access to the resource")]
    [JsonPropertyName("resourceId")]
    string? ResourceId { get; set; }

    [Description("If set to true, the reservation-flag in KRR will not be respected, and the message is sent even to persons actively objecting to the use of digital channels. Default: false")] 
    public bool DisregardKrrReservationFlag { get; set; } = false;
    
    public ChannelScheme ChannelScheme { get; set; }
    
    public EmailSettings EmailSettings { get; set; }
    
    public SMSSettings SMSSettings { get; set; }
}
