using Altinn.Notifications.NewApiDemo.api.Recipient;

using Microsoft.AspNetCore.Components;

namespace Altinn.Notifications.NewApiDemo.api.order.Request;

using System.Text.Json.Serialization;
using System.ComponentModel;


[Description("Descrition goes here")]
public class Notification
{
    [JsonPropertyName("idempotencyId")] 
    [Description("sender-scoped unique key")]
    public required string IdempotencyId { get; set; }
    
    public AssociationDialogporten? AssociationDialogporten { get; set; }
    
    [Description("Reference determined bt the sender. May be unique or non-unique")]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
    
    [Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    [JsonPropertyName("requestedSendTime")]
    public DateTime? RequestedSendTime {get;set;}   
    
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }
    
    [Description("Recipient container-object. Must be only one of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. /n" +
                 "Workaround for poor polymorphism-support in client tooling - see e.g.: https://github.com/OpenAPITools/openapi-generator/issues/10514.")]
    [JsonPropertyName("recipient")]
    //[JsonConverter(typeof(RecipientConverter))]
    public required RecipientContainer NotificationRecipient {get;set;}
    
    [JsonPropertyName("reminders")]
    public List<Reminder>? Reminders {get;set;}
    
    
}

