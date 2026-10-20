using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Creates, sends and tracks the status of email notifications.
/// </summary>
public interface IEmailNotificationService : INotificationService
{
    /// <summary>
    /// Builds in-memory email notifications for the given recipient and address points.
    /// Does not persist.
    /// </summary>
    /// <param name="orderId">The unique identifier for the order associated with the notification.</param>
    /// <param name="requestedSendTime">The time at which the notification is requested to be sent.</param>
    /// <param name="emailAddresses">The list of email addresses to send the notification to.</param>
    /// <param name="emailRecipient">The email recipient to send the notification to.</param>
    /// <param name="ignoreReservation">Indicates whether to ignore the reservation status of the recipient.</param>
    /// <returns>A read-only list of the materialized <see cref="EmailNotification"/> instances, not yet persisted.</returns>
    Task<IReadOnlyList<EmailNotification>> CreateNotification(Guid orderId, DateTime requestedSendTime, List<EmailAddressPoint> emailAddresses, EmailRecipient emailRecipient, bool ignoreReservation = false);

    /// <summary>
    /// Sends the next pending email notification, if any.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> if a notification was claimed and processed; otherwise <see langword="false"/>.</returns>
    Task<bool> SendNotification(CancellationToken cancellationToken);

    /// <summary>
    /// Sends the next pending composed email notification, if any.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> if a composed notification was claimed and processed; otherwise <see langword="false"/>.</returns>
    Task<bool> SendComposedNotification(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the send status of a notification.
    /// </summary>
    /// <param name="sendOperationResult">The result of the send operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateSendStatus(EmailSendOperationResult sendOperationResult);
}
