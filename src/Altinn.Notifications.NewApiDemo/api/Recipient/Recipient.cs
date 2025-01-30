namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "recipientType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(RecipientEmail), "email")]
[JsonDerivedType(typeof(RecipientSMS), "sms")]
[JsonDerivedType(typeof(RecipientSSN), "ssn")]
[JsonDerivedType(typeof(RecipientOrg), "orgnr")]
//[JsonDerivedType(typeof(Recipient), "failed")]
public abstract class Recipient
{
    
}


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecipientType
{
    sms,
    email,
    ssn,
    orgnr
}