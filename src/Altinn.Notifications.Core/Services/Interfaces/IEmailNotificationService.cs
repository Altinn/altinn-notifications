using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the email notification service.
/// </summary>
public interface IEmailNotificationService : INotificationService
{
    /// <summary>
    /// Creates a new email notification.
    /// </summary>
    /// <param name="orderId">The unique identifier for the order associated with the notification.</param>
    /// <param name="requestedSendTime">The time at which the notification is requested to be sent.</param>
    /// <param name="emailAddresses">The list of email addresses to send the notification to.</param>
    /// <param name="emailRecipient">The email recipient to send the notification to.</param>
    /// <param name="ignoreReservation">Indicates whether to ignore the reservation status of the recipient.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateNotification(Guid orderId, DateTime requestedSendTime, List<EmailAddressPoint> emailAddresses, EmailRecipient emailRecipient, bool ignoreReservation = false);

    /// <summary>
    /// Initiates the process of sending all ready email notifications.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendNotifications(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the send status of a notification.
    /// </summary>
    /// <param name="sendOperationResult">The result of the send operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateSendStatus(EmailSendOperationResult sendOperationResult);
}
