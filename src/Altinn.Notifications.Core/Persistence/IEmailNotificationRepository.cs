using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to an email notification
/// </summary>
public interface IEmailNotificationRepository
{
    /// <summary>
    /// Adds a new email notification to the database
    /// </summary>
    public Task AddNotification(EmailNotification notification, DateTime expiry);

    /// <summary>
    /// Retrieves all email notifications with status 'New'
    /// </summary>
    /// <returns>A list of emails</returns>
    public Task<List<Email>> GetNewNotifications();

    /// <summary>
    /// Sets result status of an email notification and update operation id
    /// </summary>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);

    /// <summary>
    /// Attempts to transition the notification order to its 'Completed' status by examining the state of all related Email and SMS notifications.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the Email notification. Can be null if the operation should be skipped.</param>
    /// <returns>
    /// <c>true</c> if the order was successfully transitioned to its final 'Completed' status;
    /// <c>false</c> if the order remains in its current status (because the associated Email and SMS notification has not reached its final status, or the order was already completed).
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
