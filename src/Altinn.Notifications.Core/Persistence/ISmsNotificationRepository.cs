using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to an sms notification
/// </summary>
public interface ISmsNotificationRepository
{
    /// <summary>
    /// Adds a new sms notification to the database
    /// </summary>
    public Task AddNotification(SmsNotification notification, DateTime expiry);

    /// <summary>
    /// Retrieves all sms notifications with status 'New'
    /// </summary>
    /// <returns>A list of sms</returns>
    public Task<List<Sms>> GetNewNotifications();

    /// <summary>
    /// Sets result status of an email notification and update operation id
    /// </summary>
    public Task UpdateSendStatus(Guid notificationId, SmsNotificationResultType result, string? gatewayReference = null);
}
