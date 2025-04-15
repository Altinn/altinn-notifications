using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Defines common delivery tracking capabilities for deliverable entities in the notification system.
/// </summary>
/// <remarks>
/// This interface extends basic status tracking with destination addressing capabilities,
/// providing the foundation for monitoring any entity that can be delivered to a recipient.
/// 
/// It represents a key abstraction in the notification tracking hierarchy that bridges
/// status information with addressing details across various communication channels.
/// The polymorphic serialization support enables type-safe handling of different delivery
/// mechanisms while maintaining a consistent tracking contract.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SmsDeliveryManifestExt), "SMS")]
[JsonDerivedType(typeof(EmailDeliveryManifestExt), "Email")]
public interface IDeliverableEntityExt : IStatusExt
{
    /// <summary>
    /// Gets the destination address where the deliverable entity is sent.
    /// </summary>
    /// <value>
    /// A string representing the recipient's address, with format depending on the delivery channel:
    /// - For email deliveries: an email address
    /// - For SMS deliveries: a mobile phone number in international format
    /// </value>
    [JsonPropertyName("destination")]
    string Destination { get; }
}
