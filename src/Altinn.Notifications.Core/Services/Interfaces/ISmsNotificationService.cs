using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines the contract for the SMS notification service.
/// </summary>
public interface ISmsNotificationService : INotificationService
{
    /// <summary>
    /// Creates a new SMS notification based on the provided order identifier, requested send time, address points, and recipient details.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order associated with the notification.</param>
    /// <param name="requestedSendTime">The date and time when the notification is requested to be sent.</param>
    /// <param name="addressPoints">A list of SMS address points containing the recipient's mobile numbers.</param>
    /// <param name="recipient">The recipient details of the SMS notification.</param>
    /// <param name="count">The number of SMS messages to be sent.</param>
    /// <param name="ignoreReservation">A flag indicating whether to ignore the recipient's reservation status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateNotification(Guid orderId, DateTime requestedSendTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, int count, bool ignoreReservation = false);

    /// <summary>
    /// Creates a new SMS notification based on the provided order identifier, requested send time, address point, and recipient details.
    /// </summary>
    /// <param name="orderId">
    /// The unique identifier of the notification order associated with the SMS.
    /// </param>
    /// <param name="requestedSendTime">
    /// The date and time when the SMS is requested to be sent.
    /// </param>
    /// <param name="recipient">
    /// The recipient details of the SMS notification, including identification information.
    /// </param>
    /// <param name="expiryDateTime">
    /// The date and time when the notification expires and will no longer be delivered.
    /// </param>
    /// <param name="smsCount">
    /// The number of SMS messages to be sent for this notification.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the creation operation..
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    Task CreateNotificationAsync(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, DateTime expiryDateTime, int smsCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates the process of sending all ready-to-send SMS notifications.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy to filter the notifications. Defaults to daytime for SMS.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendNotifications(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

    /// <summary>
    /// Updates the send status of an SMS notification based on the provided send operation result.
    /// </summary>
    /// <param name="sendOperationResult">The result of the SMS send operation, including the notification identifier and send result status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateSendStatus(SmsSendOperationResult sendOperationResult);
}
