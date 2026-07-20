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
/// <param name="NotificationId">
/// The alternate identifier of the source email or SMS notification this log entry was derived from.
/// Unique per entry; used as the idempotency key for the underlying insert.
/// </param>
/// <param name="CreatorName">The short name of the service owner that created the order.</param>
/// <param name="SendersReference">The sender's own reference for the order, or <see langword="null"/> if none was provided.</param>
/// <param name="DialogId">
/// The Dialogporten dialog identifier associated with this notification, or <see langword="null"/> when
/// the order has no Dialogporten association.
/// </param>
/// <param name="TransmissionId">
/// The Dialogporten transmission identifier associated with this notification, or <see langword="null"/>
/// when the order has no Dialogporten association.
/// </param>
/// <param name="DeliveryReference">
/// The email or SMS provider's own tracking reference for this send attempt (Azure Communication Services'
/// operation ID for email, LinkMobility's gateway reference for SMS), or <see langword="null"/> if the
/// notification was not processed by the provider.
/// </param>
/// <param name="Recipient">
/// The national identity number or organisation number of the recipient, or <see langword="null"/> when
/// the notification was addressed directly to an email address or phone number.
/// </param>
/// <param name="Type">The notification order type (e.g. <c>Notification</c>, <c>Reminder</c>, <c>Instant</c>, <c>Composed</c>).</param>
/// <param name="Channel">The notification channel (<c>Email</c> or <c>Sms</c>).</param>
/// <param name="Destination">The email address or mobile number the notification was sent to.</param>
/// <param name="Resource">The Altinn resource identifier linked to this notification.</param>
/// <param name="Status">The delivery result status at the time the log entry was created.</param>
/// <param name="RequestedSendTime">The timestamp the order requested notifications be sent at.</param>
/// <param name="LastUpdateTime">The timestamp when the notification status was last updated.</param>
public record NotificationLogEntry(
    Guid? OrderChainId,
    Guid ShipmentId,
    Guid NotificationId,
    string CreatorName,
    string? SendersReference,
    string? DialogId,
    string? TransmissionId,
    string? DeliveryReference,
    string? Recipient,
    string Type,
    string Channel,
    string Destination,
    string? Resource,
    string Status,
    DateTime RequestedSendTime,
    DateTime LastUpdateTime);
