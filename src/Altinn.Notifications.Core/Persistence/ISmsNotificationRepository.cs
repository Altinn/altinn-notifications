using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Defines the repository operations related to SMS notifications.
/// </summary>
public interface ISmsNotificationRepository : INotificationRepository
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
    /// Retrieves pending SMS notifications that are eligible under the specified sending time policy.
    /// </summary>
    /// <param name="publishBatchSize">
    /// Maximum number of SMS notifications to retrieve in a single batch. Controls how many notifications 
    /// will be transitioned from "new" to "sending" status and published to Kafka.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation.
    /// </param>
    /// <param name="sendingTimePolicy">
    /// Policy that determines which notifications are eligible for retrieval (for example, Daytime or Anytime).
    /// </param>
    /// <returns>
    /// A task that completes when retrieval finishes (no more eligible items or the retrieval window ends) or when cancellation is requested.
    /// The task result contains a list of SMS notifications to be processed, limited by the specified batch size.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if cancellation is requested before or during retrieval.
    /// </exception>
    Task<List<Sms>> GetNewNotifications(int publishBatchSize, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

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
