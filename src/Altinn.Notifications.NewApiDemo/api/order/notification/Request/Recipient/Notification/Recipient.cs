using Microsoft.OpenApi.Extensions;

namespace Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

using System.Text.Json.Serialization;
using System.ComponentModel;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "recipientType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(RecipientEmail), "email")]
[JsonDerivedType(typeof(RecipientSms), "sms")]
[JsonDerivedType(typeof(RecipientNationalIdentityNumber), "nationalIdentityNumber")]
[JsonDerivedType(typeof(RecipientOrg), "org")]
public abstract class Recipient
{
    
}


//just for docs
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecipientType
{
    sms,
    email,
    nationalIdentityNumber,
    org
}
