namespace Altinn.Notifications.Core.Models.Delivery;

/// <summary>
/// Represents tracking information for SMS notifications delivered to a specific recipient.
/// </summary>
/// <remarks>
/// This record provides a specialized implementation for tracking SMS-based notifications,
/// with the destination property representing a recipient's mobile phone number in international format.
/// 
/// As a concrete implementation of <see cref="DeliverableEntity"/>, this class inherits
/// standardized tracking properties including status information and timestamp management.
/// It's identified by the "SMS" type discriminator in polymorphic serialization to enable
/// proper type handling when processing notification tracking data.
/// </remarks>
public record SmsDeliveryManifest : DeliverableEntity
{
    // SMS-specific implementation inherits all required functionality from the base class.
}
