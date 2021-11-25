using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core
{
    public interface INotificationsRepository
    {
        Task<Notification> SaveNotification(Notification notification);
    }
}
