namespace Altinn.Notifications.Tools.DlqManager.Repositories;

/// <summary>
/// Targeted database queries for the SMS send DLQ operations.
/// </summary>
public interface ISmsNotificationRepository
{
    /// <summary>
    /// Returns the current <c>result</c>, <c>expirytime</c>, <c>isExpired</c>, and <c>resulttime</c>
    /// for the given notification. <c>IsExpired</c> is computed by the database using <c>NOW()</c>
    /// so expiry evaluation uses the same clock as the data.
    /// Returns <c>(null, null, false, null)</c> when no row is found.
    /// </summary>
    Task<(string? Result, DateTime? ExpiryTime, bool IsExpired, DateTime? ResultTime)> GetNotificationStateAsync(Guid notificationId);

    /// <summary>
    /// Bulk variant of <see cref="GetNotificationStateAsync"/> — fetches state for all
    /// <paramref name="notificationIds"/> in a single query.
    /// Returns a dictionary keyed by notification id; ids with no matching row are absent.
    /// </summary>
    Task<Dictionary<Guid, (string? Result, DateTime? ExpiryTime, bool IsExpired, DateTime? ResultTime)>> GetNotificationStatesAsync(IReadOnlyList<Guid> notificationIds);

    /// <summary>
    /// Sets <c>result = 'Accepted'</c> and <c>resulttime = NOW()</c> for the notification,
    /// but only when the current result is <c>'Sending'</c> and <c>expirytime &lt; NOW()</c>.
    /// </summary>
    /// <returns>Number of rows updated (0 = wrong state, not yet expired, or not found; 1 = updated).</returns>
    Task<int> UpdateResultToAcceptedAsync(Guid notificationId);
}
