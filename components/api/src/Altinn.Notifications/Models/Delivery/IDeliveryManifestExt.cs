using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents standardized destination and status information for trackable entities within the notification system.
/// </summary>
/// <remarks>
/// This interface provides a structured approach to track the current delivery status and its associated destination
/// information for entities that are to be delivered across various channels (e.g., email, SMS), regardless of the payload.
/// 
/// It supports polymorphic serialization, enabling type-safe tracking of different delivery mechanisms while ensuring
/// consistency in the reporting of delivery status across channels.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SmsDeliveryManifestExt), "SMS")]
[JsonDerivedType(typeof(EmailDeliveryManifestExt), "Email")]
public interface IDeliveryManifestExt : IStatusExt
{
    /// <summary>
    /// Gets the destination address where the deliverable entity is intended to be sent.
    /// </summary>
    /// <value>
    /// A string representing the recipient's address. The format depends on the delivery channel:
    /// - For email: an email address  
    /// - For SMS: a mobile phone number in international format
    /// </value>
    [JsonPropertyName("destination")]
    string Destination { get; }
}
