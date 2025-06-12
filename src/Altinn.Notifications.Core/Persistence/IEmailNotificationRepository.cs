using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Models.Status;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to an email notification
/// </summary>
public interface IEmailNotificationRepository
{
    /// <summary>
    /// Adds a new email notification to the database
    /// </summary>
    public Task AddNotification(EmailNotification notification, DateTime expiry);

    /// <summary>
    /// Retrieves all email notifications with status 'New'
    /// </summary>
    /// <returns>A list of emails</returns>
    public Task<List<Email>> GetNewNotifications();

    /// <summary>
    /// Sets result status of an email notification and update operation id
    /// </summary>
    public Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null);

    /// <summary>
    /// Retrieves all processed email recipients for an order
    /// </summary>
    /// <returns>A list of email recipients</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid orderId);
    
    /// <summary>
    /// Terminates any active notifications that have exceeded their expected duration or are no longer valid.
    /// </summary>
    /// <remarks>This method is typically used to clean up notifications that are stuck in a hanging state. It
    /// ensures that resources associated with such notifications are released properly.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task TerminateExpiredNotifications();
}
