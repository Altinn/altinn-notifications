using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the in-memory result of processing an SMS-only notification order.
/// Returned by the channel processing service instead of persisting eagerly;
/// handed to the repository layer for atomic persistence.
/// </summary>
public sealed record SmsOrderProcessingResult(
    List<SmsNotification> Notifications, 
    DateTime? ExpirationDateTime);
