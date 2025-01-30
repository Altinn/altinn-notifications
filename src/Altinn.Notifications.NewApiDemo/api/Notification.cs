using Microsoft.AspNetCore.Components;

namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;


[Description("Descrition goes here")]
public class Notification
{
    
    [Description("Type")]
    [JsonPropertyName("notificationType")]
    public required NotificationType NotificationType { get; set; } 
    
    //[Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    //public DateTime? NotBefore {get;set;}
    
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }
    
    [Description("The resource that the notification is related to, and that recipient elegebility will be evaluated on (e.g. when sending to an org. no will result in a notification to the official contact and only individuals with custom notifications AND access to the resource")]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }
    
    public string? Associations { get; set; }
    
    [Description("Reference determined bt the sender. May be unique or non-unique")]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
    
    [Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    [JsonPropertyName("notBefore")]
    public DateTime? NotBefore {get;set;}  //evt. "DaysToDelay"
    
    [Description("Date and time this notification can be sent at the earliest. If not set, this defaults to 'now()'. The Notification will be processed on or as soon as possible after this time, in accordance with the selected policy.")]
    [JsonPropertyName("delayDays")]
    public int? DelayDays {get;set;}  //evt. "DaysToDelay"
    
    [JsonPropertyName("recipient")]
    public required Recipient Recipient {get;set;}
    
    /*
    "requestedSendTime": "2025-01-10T14:54:17.128Z",
    "sendersReference": "string",
    "recipients": [
    {
        "emailAddress": "string",
        "mobileNumber": "string",
        "organizationNumber": "string",
        "nationalIdentityNumber": "string",
        "isReserved": true
    }
    ],
    */
    /*
    {
        "serviceResource": "urn:altinn:resource:ske_tredjepartsopplysninger_boligsameier",
        "party": "urn:altinn:organization:identifier-no:912345678",
        // associatedWith brukes av arbeidsflate/SBSer for å finne varslingsordrer relatert til en
        // dialog
        "associatedWith": [
        { "type": "urn:altinn:dialogporten:dialog-id", "id": "{{dialog-guid}}" }
        { "type": "urn:altinn:dialogporten:transmission-id", "id": "{{transmissionId-utsending-1}}" }
        ],
        "channel": "both",
        "content": [  ....  ]
    }
    */
}

