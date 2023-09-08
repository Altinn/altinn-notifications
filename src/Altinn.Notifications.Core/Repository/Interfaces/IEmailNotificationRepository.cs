using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Repository.Interfaces;

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
    public Task UpdateSendStatus(Guid notificationId, EmailNotificationResultType status, string? operationId);

    /// <summary>
    /// Retrieves all email recipients for an order
    /// </summary>
    /// <returns>A list of emails</returns>
    public Task<List<EmailRecipient>> GetRecipients(Guid notificationId);
}