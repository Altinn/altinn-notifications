using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WebApplication1
{
    public class Reminder
    {
        [Description("Reference determined bt the sender. May be unique or non-unique")]
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }
        
        [JsonPropertyName("conditionEndpoint")]
        public Uri? ConditionEndpoint { get; set; }
        
        [Description("The Reminder will be processed on or as soon as possible after the Notification RequestedSendTime + this number of 24-hour increments, in accordance with the selected policy.")]
        [JsonPropertyName("delayDays")]
        public int? RequestedSendTimeDelayDays {get;set;}  
        
        //TODO: FInd a way to add this without getting 'System.ArgumentException: An item with the same key has already been added. Key: RecipientRecipientEmail
                
        [JsonPropertyName("recipient")]
        public required Altinn.Notifications.NewApiDemo.api.Recipient.Reminder.Recipient ReminderRecipient {get;set;}
        
    }
}
