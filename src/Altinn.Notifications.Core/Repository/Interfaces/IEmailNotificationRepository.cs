using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;

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
    /// Sets result status of an email
    /// </summary>
    public Task SetResultStatus(int emailId, EmailNotificationResultType status);
}