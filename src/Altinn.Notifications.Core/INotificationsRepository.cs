using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core
{
    public interface INotificationsRepository
    {
        Task<Notification> AddNotification(Notification notification);

        Task<Target> AddTarget(Target target);

        Task<Message> AddMessage(Message message);
    }
}
