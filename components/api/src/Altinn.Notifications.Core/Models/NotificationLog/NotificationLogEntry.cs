namespace Altinn.Notifications.Core.Models.NotificationLog;

/// <summary>
/// Represents a notification log entry to be persisted when a shipment reaches a final status.
/// </summary>
public sealed record NotificationLogEntry
{
    /// <summary>
    /// Gets the unique identifier for the notification shipment.
    /// </summary>
    public required Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the type of notification (e.g., 'Email', 'SMS').
    /// </summary>
    public required string NotificationType { get; init; }

    /// <summary>
    /// Gets the optional order chain identifier.
    /// </summary>
    public long? OrderChainId { get; init; }

    /// <summary>
    /// Gets the optional dialog identifier associated with the notification.
    /// </summary>
    public Guid? DialogId { get; init; }

    /// <summary>
    /// Gets the optional transmission identifier.
    /// </summary>
    public string? TransmissionId { get; init; }

    /// <summary>
    /// Gets the optional Azure Communication Services operation identifier.
    /// </summary>
    public string? OperationId { get; init; }

    /// <summary>
    /// Gets the optional SMS gateway reference.
    /// </summary>
    public string? GatewayReference { get; init; }

    /// <summary>
    /// Gets the optional recipient identifier (organization number or national identity number).
    /// </summary>
    public string? Recipient { get; init; }

    /// <summary>
    /// Gets the optional destination address (email address or phone number).
    /// </summary>
    public string? Destination { get; init; }

    /// <summary>
    /// Gets the optional resource associated with the notification.
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Gets the optional final status of the notification.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the optional timestamp when the notification was sent.
    /// </summary>
    public DateTime? SentTimestamp { get; init; }
}
