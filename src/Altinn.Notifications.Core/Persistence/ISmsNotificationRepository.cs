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
}
