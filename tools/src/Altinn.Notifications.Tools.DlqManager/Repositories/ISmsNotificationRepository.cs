namespace Altinn.Notifications.Tools.DlqManager.Repositories;

/// <summary>
/// Targeted database queries for the SMS send DLQ operations.
/// </summary>
public interface ISmsNotificationRepository
{
    /// <summary>
    /// Returns the current <c>result</c>, <c>expirytime</c>, and <c>resulttime</c>
    /// for the given notification. Returns <c>(null, null, null)</c> when no row is found.
    /// </summary>
    Task<(string? Result, DateTime? ExpiryTime, DateTime? ResultTime)> GetNotificationStateAsync(Guid notificationId);

    /// <summary>
    /// Sets <c>result = 'Accepted'</c> and <c>resulttime = NOW()</c> for the notification,
    /// but only when the current result is not already a terminal state.
    /// </summary>
    /// <returns>Number of rows updated (0 = already terminal or not found, 1 = updated).</returns>
    Task<int> UpdateResultToAcceptedAsync(Guid notificationId);
}
