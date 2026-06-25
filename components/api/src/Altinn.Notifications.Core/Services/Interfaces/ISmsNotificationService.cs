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
    /// Sends pending SMS notifications that are eligible under the specified sending time policy.
    /// </summary>
    Task SendNotifications(CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime);

    /// <summary>
    /// Updates the send status of an SMS notification based on the provided send operation result.
    /// </summary>
    Task UpdateSendStatus(SmsSendOperationResult sendOperationResult);

    /// <summary>
    /// Builds in-memory SMS notifications for the given recipient and address points.
    /// Does not persist. Expiry time is computed internally.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order associated with the notification.</param>
    /// <param name="requestedSendTime">The date and time when the notification is requested to be sent.</param>
    /// <param name="expiryDateTime">The date and time when the notification expires and should no longer be sent.</param>
    /// <param name="addressPoints">A list of SMS address points containing the recipient's mobile numbers.</param>
    /// <param name="recipient">The recipient details of the SMS notification.</param>
    /// <param name="count">The number of SMS messages to be sent.</param>
    /// <param name="ignoreReservation">A flag indicating whether to ignore the recipient's reservation status for receiving SMS notifications.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<PendingSmsNotification>> CreateNotification(Guid orderId, DateTime requestedSendTime, DateTime expiryDateTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, int count, bool ignoreReservation = false);
}
