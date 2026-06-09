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
    /// Updates the send status of an SMS notification based on the provided send operation result.
    /// </summary>
    /// <param name="sendOperationResult">The result of the SMS send operation, including the notification identifier and send result status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateSendStatus(SmsSendOperationResult sendOperationResult);

    /// <summary>
    /// Sends pending SMS notifications that are eligible under the specified sending time policy.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation.
    /// </param>
    /// <param name="sendingTimePolicy">
    /// Policy that determines which notifications are eligible for processing (for example, Daytime or Anytime).
    /// </param>
    /// <returns>
    /// A task that completes when processing finishes (no more eligible items or the processing window ends) or when cancellation is requested.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if cancellation is requested before or during processing.
    /// </exception>
    Task SendNotifications(CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

    /// <summary>
    /// Creates a new SMS notification based on the provided order identifier, requested send time, expiry time, address points, and recipient details.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order associated with the notification.</param>
    /// <param name="requestedSendTime">The date and time when the notification is requested to be sent.</param>
    /// <param name="expiryDateTime">The date and time when the notification expires and should no longer be sent.</param>
    /// <param name="addressPoints">A list of SMS address points containing the recipient's mobile numbers.</param>
    /// <param name="recipient">The recipient details of the SMS notification.</param>
    /// <param name="count">The number of SMS messages to be sent.</param>
    /// <param name="ignoreReservation">A flag indicating whether to ignore the recipient's reservation status for receiving SMS notifications.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateNotification(Guid orderId, DateTime requestedSendTime, DateTime expiryDateTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, int count, bool ignoreReservation = false);
}
