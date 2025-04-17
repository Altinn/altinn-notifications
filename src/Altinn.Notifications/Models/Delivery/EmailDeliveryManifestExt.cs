namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents tracking information for email notifications sent to a specific recipient.
/// </summary>
/// <remarks>
/// This record provides a specialized implementation for tracking email-based notifications,
/// with the destination property representing a recipient's email address.
/// 
/// This record is identified by the "Email" type discriminator in polymorphic serialization,
/// ensuring correct type handling during the processing of notification tracking data.
/// </remarks>
public record EmailDeliveryManifestExt : DeliveryManifestExt
{
    // Email-specific implementation inherits all required functionality from the base class.
}
