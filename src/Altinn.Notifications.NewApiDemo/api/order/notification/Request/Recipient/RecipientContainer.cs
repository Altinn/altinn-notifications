using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.Recipient
{
    
    //workaround for poor polymorphism-support in client tooling - see e.g.: https://github.com/OpenAPITools/openapi-generator/issues/10514
    [Description("Recipient container-object. Must be only one of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. /n" +
                 "Workaround for poor polymorphism-support in client tooling - see e.g.: https://github.com/OpenAPITools/openapi-generator/issues/10514.")]
    public class RecipientContainer
    {
        //[Description("Recipient-object. One of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. See /dummy/contracts/recipient for schemas.")]
        [JsonPropertyName("recipientEmail")]
        public RecipientEmail? RecipientEmail { get; set; }
        
        //[Description("Recipient-object. One of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. See /dummy/contracts/recipient for schemas.")]
        [JsonPropertyName("recipientSms")]
        public RecipientSms? RecipientSms { get; set; }
        
        //[Description("Recipient-object. One of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. See /dummy/contracts/recipient for schemas.")]
        [JsonPropertyName("recipientNationalIdentityNumber")]
        public RecipientNationalIdentityNumber? RecipientNationalIdentityNumber { get; set; }
        
        //[Description("Recipient-object. One of the following: RecipientEmail, RecipientNationalIdentityNumber, RecipientOrd or RecipientSMS. See /dummy/contracts/recipient for schemas.")]
        [JsonPropertyName("recipientOrg")]
        public RecipientOrg? RecipientOrg { get; set; }
    }
}
