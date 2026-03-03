namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Specifies the stages of order processing within the system.
/// </summary>
/// <remarks>Use this enumeration to indicate whether an order is in the registration or processing phase. This
/// distinction enables clear tracking and management of an order's lifecycle.</remarks>
public enum OrderPhase
{
    /// <summary>
    /// Signifies that this is happening through the order registration phase.
    /// </summary>
    Registration,

    /// <summary>
    /// Happens when the order is picked up for further processing by the consumer.
    /// </summary>
    Processing
}
