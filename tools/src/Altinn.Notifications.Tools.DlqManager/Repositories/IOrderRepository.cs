namespace Altinn.Notifications.Tools.DlqManager.Repositories;

/// <summary>
/// Provides targeted database queries for the past due orders DLQ operations.
/// Only contains the lookups needed by the DLQ Manager — deliberately minimal
/// to avoid coupling to the full Persistence component.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Returns the current DB state for the given order:
    /// <c>processedstatus</c>, total notification count (SMS + email),
    /// the canonical expiry time (<c>requestedsendtime + 48 h</c>), and whether
    /// that expiry has already passed.
    /// Returns <c>(null, 0, null, false)</c> when the order is not found in the database.
    /// </summary>
    Task<(string? Status, long NotificationCount, DateTime? ExpiryTime, bool IsExpired)> GetOrderStateAsync(Guid orderId);

    /// <summary>
    /// Bulk variant of <see cref="GetOrderStateAsync"/> for inspecting multiple orders in one round trip.
    /// Orders not found in the database are omitted from the result dictionary.
    /// </summary>
    Task<Dictionary<Guid, (string? Status, long NotificationCount, DateTime? ExpiryTime, bool IsExpired)>> GetOrderStatesAsync(
        IReadOnlyList<Guid> orderIds);
}
