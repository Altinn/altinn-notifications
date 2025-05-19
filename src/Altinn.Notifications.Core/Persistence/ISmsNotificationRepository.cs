using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Defines the repository operations related to SMS notifications.
/// </summary>
public interface ISmsNotificationRepository
{
    /// <summary>
    /// Adds a new SMS notification to the database.
    /// </summary>
    /// <param name="notification">The SMS notification to be added.</param>
    /// <param name="expiry">The expiration date and time of the notification.</param>
    /// <param name="count">The number of SMS messages.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddNotification(SmsNotification notification, DateTime expiry, int count);

    /// <summary>
    /// Retrieves all SMS notifications that have the status 'New'.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy to filter the notifications. Defaults to daytime for sms</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of new SMS notifications.</returns>
    Task<List<Sms>> GetNewNotifications(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

    /// <summary>
    /// Retrieves all processed SMS recipients for a specified order.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of SMS recipients.</returns>
    Task<List<SmsRecipient>> GetRecipients(Guid orderId);

    /// <summary>
    /// Updates the send status of an SMS notification and sets the operation identifier.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the SMS notification.</param>
    /// <param name="result">The result status of the SMS notification.</param>
    /// <param name="gatewayReference">The gateway reference (optional).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateSendStatus(Guid? notificationId, SmsNotificationResultType result, string? gatewayReference = null);

    /// <summary>
    /// Attempts to transition the notification order to its 'Completed' status by examining the state of all related SMS and Email notifications.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the SMS notification. Can be null if the operation should be skipped.</param>
    /// <returns>
    /// <c>true</c> if the order was successfully transitioned to its final 'Completed' status;
    /// <c>false</c> if the order remains in its current status (because the associated SMS and Email notification has not reached its final status, or the order was already completed).
    /// </returns>
    /// <remarks>
    /// <para>
    /// The order will only transition to 'Completed' status if all of its associated SMS and Email notifications have reached terminal states 
    /// (neither 'New', 'Sending', 'Accepted' for SMS nor 'New', 'Sending', 'Succeeded' for Email notifications).
    /// </para>
    /// <para>
    /// This operation is idempotent - calling it multiple times on a notification that's already in its final status will return <c>false</c> 
    /// indicating no change was needed. The underlying database function uses row locking to ensure thread safety.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if a database error occurs during status transition.</exception>
    public Task<bool> TryTransitionOrderToFinalStatus(Guid? notificationId);
}
