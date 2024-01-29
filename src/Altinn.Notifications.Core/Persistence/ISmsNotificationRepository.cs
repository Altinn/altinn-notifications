using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface describing all repository operations related to an sms notification
/// </summary>
public interface ISmsNotificationRepository
{
    /// <summary>
    /// Retrieves all sms notifications with status 'New'
    /// </summary>
    /// <returns>A list of sms</returns>
    public Task<List<Sms>> GetNewNotifications();
}
