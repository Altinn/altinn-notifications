using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api_shared.shipment
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ShipmentStatusType
    {
        //Common codes
        [Description("The email has been created but has not yet been picked up for processing.")]
        New,
        
        [Description("The email is being processed and will be sent shortly.")]
        Sending,	
        
        [Description("The email was successfully delivered to the recipient. No errors were reported, indicating successful delivery.")]
        Delivered,	
        
        [Description("The email was not sent due to an unspecified failure.")]
        Failed,	
        
        [Description("The SMS was not sent because the recipient’s SMS address was not found.")]
        Failed_RecipientNotIdentified,
        
        
        
        //Email-specific status codes
        
        [Description("The email has been accepted by the third-party service and will be sent soon.")]
        Succeeded,	
        
        [Description("The email was not sent due to an invalid email address format.")]
        Failed_InvalidEmailFormat,
        
        [Description("The email bounced due to issues like a non-existent email address or invalid domain.")]
        Failed_Bounced,
        
        [Description("The email was identified as spam and rejected or blocked (not quarantined).")]
        Failed_FilteredSpam,
        
        [Description("The email was quarantined due to being flagged as spam, bulk mail, or phishing.")]
        Failed_Quarantined	,
        
        
        
        
        //Codes only relevant for SMS
        [Description("The SMS has been accepted by the gateway service and will be sent soon.")]
        Accepted,	
        
        [Description("The SMS was not delivered because the recipient’s mobile number is barred, blocked or not in use.")]
        Failed_BarredReceiver,	
        
        [Description("The SMS was not delivered because the message has been deleted.")]
        Failed_Deleted,	
        
        [Description("The SMS was not delivered because it has been expired.")]
        Failed_Expired,
        
        [Description("The SMS was not sent because the recipient mobile number was invalid.")]
        Failed_InvalidRecipient,
        
        [Description("The SMS was not delivered due to invalid mobile number or no available route to destination.")]
        Failed_Undelivered,
        
        [Description("The SMS was not delivered because it was rejected.")]
        Failed_Rejected	
    }
}
