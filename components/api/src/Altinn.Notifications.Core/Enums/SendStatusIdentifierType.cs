namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Identifies which key was used to perform the status update.
/// </summary>
public enum SendStatusIdentifierType
{
    /// <summary>
    /// The notification identifier.
    /// </summary>
    NotificationId,

    /// <summary>
    /// Azure Communication Services operation identifier (Email).
    /// </summary>
    OperationId,

    /// <summary>
    /// LinkMobility gateway reference (SMS).
    /// </summary>
    GatewayReference
}
