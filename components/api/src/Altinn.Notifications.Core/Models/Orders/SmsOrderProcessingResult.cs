using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the in-memory result of processing an SMS notification order.
/// Returned by the channel processing service instead of persisting eagerly;
/// handed to the repository layer for atomic persistence.
/// </summary>
/// <param name="Notifications">The SMS notifications built during order processing.</param>
/// <param name="ExpirationDateTime">
/// The point in time after which unsent notifications may be discarded.
/// Computed by <c>SmsOrderProcessingService.GetExpirationDateTime</c>:
/// <list type="bullet">
///   <item><description><c>Anytime</c> (and all other policies): <c>requestedSendTime + 48h</c>.</description></item>
///   <item><description><c>Daytime</c>: based on the Norwegian send window — 48h when the send time
///   falls within the window, or anchored to the next window start and extended by 48h or 72h
///   depending on whether the send time is before or after the window.</description></item>
/// </list>
/// <c>null</c> is only valid when all notifications are in a terminal state and will never be
/// picked up by the send pipeline. The repository falls back to <c>DateTime.UtcNow</c> if not provided.
/// </param>
public sealed record SmsOrderProcessingResult(
    IReadOnlyList<SmsNotification> Notifications,
    DateTime? ExpirationDateTime);
