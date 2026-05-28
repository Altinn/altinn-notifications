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
    /// Sets <c>result = 'Accepted'</c> and <c>resulttime = NOW()</c> for the notification,
    /// but only when the current result is <c>'Sending'</c> and <c>expirytime &lt; NOW()</c>.
    /// </summary>
    /// <returns>Number of rows updated (0 = wrong state, not yet expired, or not found; 1 = updated).</returns>
    Task<int> UpdateResultToAcceptedAsync(Guid notificationId);
}
