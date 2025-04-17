namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents tracking information for email notifications delivered to a specific recipient.
/// </summary>
/// <remarks>
/// This record provides a specialized implementation for tracking email-based notifications,
/// with the destination property representing a recipient's email address.
/// 
/// As a concrete implementation of <see cref="DeliveryStatusExt"/>, this class inherits
/// standardized tracking properties including status information and timestamp management.
/// It's identified by the "Email" type discriminator in polymorphic serialization to enable
/// proper type handling when processing notification tracking data.
/// </remarks>
public record EmailDeliveryManifestExt : DeliveryStatusExt
{
    // Email-specific implementation inherits all required functionality from the base class.
}
