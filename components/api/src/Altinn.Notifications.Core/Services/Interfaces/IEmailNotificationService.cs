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
    /// Builds in-memory email notifications for the given recipient and address points.
    /// Does not persist. Expiry time is computed internally.
    /// </summary>
    /// <param name="orderId">The unique identifier for the order associated with the notification.</param>
    /// <param name="requestedSendTime">The time at which the notification is requested to be sent.</param>
    /// <param name="emailAddresses">The list of email addresses to send the notification to.</param>
    /// <param name="emailRecipient">The email recipient to send the notification to.</param>
    /// <param name="ignoreReservation">Indicates whether to ignore the reservation status of the recipient.</param>
    /// <returns>A read-only list of the materialized <see cref="PendingEmailNotification"/> instances, not yet persisted.</returns>
    Task<IReadOnlyList<PendingEmailNotification>> CreateNotification(Guid orderId, DateTime requestedSendTime, List<EmailAddressPoint> emailAddresses, EmailRecipient emailRecipient, bool ignoreReservation = false);

    /// <summary>
    /// Sends pending email notifications.
    /// </summary>
    Task SendNotifications(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the send status of an email notification based on the provided send operation result.
    /// </summary>
    Task UpdateSendStatus(EmailSendOperationResult sendOperationResult);
}
