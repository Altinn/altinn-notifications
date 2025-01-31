using Microsoft.AspNetCore.Components;

namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;


[Description("Descrition goes here")]
public class Notification
{
    [JsonPropertyName("idempotencyId")] 
    [Description("sender-scoped unique key")]
    public required string IdempotencyId { get; set; }
    
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }
    
    [Description("The resource that the notification is related to, and that recipient elegebility will be evaluated on (e.g. when sending to an org. no will result in a notification to the official contact and only individuals with custom notifications AND access to the resource")]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }
    
    
    public AssociationDialogporten? AssociationDialogporten { get; set; }
    
    [Description("Reference determined bt the sender. May be unique or non-unique")]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
    
    [Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    [JsonPropertyName("notBefore")]
    public DateTime? RequestedSendTime {get;set;}   
    
    
    
    [JsonPropertyName("recipient")]
    public required Recipient Recipient {get;set;}
    
    [JsonPropertyName("reminders")]
    public List<Reminder>? Reminders {get;set;}
    
    
}

