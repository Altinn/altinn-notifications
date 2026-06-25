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
