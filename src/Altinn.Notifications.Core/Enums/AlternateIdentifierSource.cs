namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Defines the source type for an alternate identifier used in notification operations.
/// </summary>
public enum AlternateIdentifierSource
{
    /// <summary>
    /// The identifier is associated with an SMS notification.
    /// </summary>
    Sms,

    /// <summary>
    /// The identifier is associated with an email notification.
    /// </summary>
    Email,

    /// <summary>
    /// The identifier is associated with an order.
    /// </summary>
    Order
}
