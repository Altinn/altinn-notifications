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
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of new SMS notifications.</returns>
    Task<List<Sms>> GetNewNotifications(SendingTimePolicy sendingTimePolicy);

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
}
