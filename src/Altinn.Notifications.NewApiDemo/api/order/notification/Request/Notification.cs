using Altinn.Notifications.NewApiDemo.logic;
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
    
    public AssociationDialogporten? AssociationDialogporten { get; set; }
    
    [Description("Reference determined bt the sender. May be unique or non-unique")]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
    
    [Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    [JsonPropertyName("requestedSendTime")]
    public DateTime? RequestedSendTime {get;set;}   
    
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }
    
    [Description("Recipient-object. One of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. See /dummy/contracts/recipient for schemas.")]
    [JsonPropertyName("recipient")]
    [JsonConverter(typeof(RecipientConverter))]
    public required Object NotificationRecipient {get;set;}
    
    [JsonPropertyName("reminders")]
    public List<Reminder>? Reminders {get;set;}
    
    
}

