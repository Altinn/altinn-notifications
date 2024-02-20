using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

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
    /// Retrieves all processed sms recipients for an order
    /// </summary>
    /// <returns>A list of sms recipients</returns>
    public Task<List<SmsRecipient>> GetRecipients(Guid orderId);

    /// <summary>
    /// Sets result status of an sms notification and update operation id
    /// </summary>
    public Task UpdateSendStatus(Guid? notificationId, SmsNotificationResultType result, string? gatewayReference = null);
}
