using System.ComponentModel;

namespace Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

using System.Text.Json.Serialization;

//[JsonDerivedType(typeof(Derived), typeDiscriminatorId: "derived")]
//[JsonPolymorphic(TypeDiscriminatorPropertyName = "recipientType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
//[JsonDerivedType(typeof(RecipientEmail), typeDiscriminator: "email")]
public class RecipientEmail: Recipient
{
    [Description("The destination email address")]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }
    
    [JsonPropertyName("emailSettings")]
    public required EmailSettings EmailSettings { get; set; }
    
}
