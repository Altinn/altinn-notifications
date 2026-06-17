namespace Altinn.Notifications.Core.Models.NotificationLog;

/// <summary>
/// Represents a single entry in the notification log, capturing the full delivery context
/// of one email or SMS notification at the point it was recorded.
/// </summary>
/// <param name="OrderChainId">
/// The database identifier of the order chain this shipment belongs to, or <see langword="null"/> for
/// standalone orders that are not part of a chain.
/// </param>
/// <param name="ShipmentId">
/// The alternate identifier of the notification order (shipment) that produced this log entry.
/// </param>
/// <param name="CreatorName">The short name of the service owner that created the order.</param>
/// <param name="DialogId">
/// The Dialogporten dialog identifier associated with this notification, or <see langword="null"/> when
/// the order has no Dialogporten association.
/// </param>
/// <param name="TransmissionId">
/// The Dialogporten transmission identifier associated with this notification, or <see langword="null"/>
/// when the order has no Dialogporten association.
/// </param>
/// <param name="OperationId">
/// The operation identifier returned by the email gateway, or <see langword="null"/> for SMS notifications
/// and for email notifications that have not yet been processed.
/// </param>
/// <param name="GatewayReference">
/// The gateway reference returned by the SMS gateway, or <see langword="null"/> for email notifications
/// and for SMS notifications that have not yet been processed.
/// </param>
/// <param name="Recipient">
/// The national identity number or organisation number of the recipient, or <see langword="null"/> when
/// the notification was addressed directly to an email address or phone number.
/// </param>
/// <param name="Type">The notification order type (e.g. <c>Email</c> or <c>Sms</c>).</param>
/// <param name="Destination">The email address or mobile number the notification was sent to.</param>
/// <param name="Resource">The Altinn resource identifier linked to this notification.</param>
/// <param name="Status">The delivery result status at the time the log entry was created.</param>
/// <param name="CreatedTimestamp">The UTC timestamp when the log entry was created.</param>
/// <param name="SentTimestamp">
/// The UTC timestamp when the notification was sent, or <see langword="null"/> if not yet sent.
/// </param>
public record NotificationLogEntry(
    long? OrderChainId,
    Guid ShipmentId,
    string? CreatorName,
    Guid? DialogId,
    string? TransmissionId,
    string? OperationId,
    string? GatewayReference,
    string? Recipient,
    string Type,
    string? Destination,
    string? Resource,
    string? Status,
    DateTime CreatedTimestamp,
    DateTime? SentTimestamp);
