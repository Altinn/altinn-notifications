namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents tracking information for SMS notifications sent to a specific recipient.
/// </summary>
/// <remarks>
/// This record provides a specialized implementation for tracking SMS-based notifications,
/// with the destination property representing a recipient's mobile phone number in international format.
/// 
/// This record is identified by the "SMS" type discriminator in polymorphic serialization, ensuring correct
/// type handling during the processing of notification tracking data.
/// </remarks>
public record SmsDeliveryManifestExt : DeliveryManifestExt
{
    // SMS-specific implementation inherits all required functionality from the base class.
}
