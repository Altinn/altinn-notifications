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
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddNotification(SmsNotification notification, DateTime expiry);

    /// <summary>
    /// Retrieves the next pending SMS notification that is eligible under the specified sending time policy.
    /// </summary>
    /// <param name="unitOfWork">
    /// The unit of work that provides the active connection and transaction.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation.
    /// </param>
    /// <param name="sendingTimePolicy">
    /// Policy that determines which notifications are eligible for retrieval.
    /// </param>
    /// <returns>
    /// A task that completes when retrieval finishes.
    /// The result is the next pending SMS notification, or <see langword="null"/> when none are available.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if cancellation is requested before or during retrieval.
    /// </exception>
    Task<Sms?> GetNewNotification(UnitOfWork unitOfWork, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

    /// <summary>
    /// Retrieves pending SMS notifications that are eligible under the specified sending time policy.
    /// </summary>
    /// <param name="publishBatchSize">Maximum number of notifications to retrieve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="sendingTimePolicy">Policy that determines which notifications are eligible for retrieval.</param>
    /// <returns>A task with a list of claimed SMS notifications.</returns>
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
    /// <param name="notificationId">The unique identifier of the SMS notification (optional if <paramref name="gatewayReference"/> is provided).</param>
    /// <param name="result">The result status of the SMS notification.</param>
    /// <param name="gatewayReference">The gateway reference from the SMS provider (optional if <paramref name="notificationId"/> is provided).</param>
    /// <param name="deliveryReport">The raw delivery report payload received from the SMS gateway (optional).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="Exceptions.InvalidNotificationIdentifierException">
    /// Thrown when both <paramref name="notificationId"/> and <paramref name="gatewayReference"/> are null or empty.
    /// </exception>
    Task UpdateSendStatus(Guid? notificationId, SmsNotificationResultType result, string? gatewayReference = null, string? deliveryReport = null);
}
