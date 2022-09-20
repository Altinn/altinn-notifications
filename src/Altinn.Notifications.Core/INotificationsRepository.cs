using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core
{
    public interface INotificationsRepository
    {
        Task<Notification> AddNotification(Notification notification);
        
        Task<Notification?> GetNotification(int id);

        Task<Target> AddTarget(Target target);

        Task UpdateSentTarget(int id);

        Task<Target?> GetTarget(int id);
      
        Task<Message> AddMessage(Message message);

        Task<List<Target>> GetUnsentTargets();
    }
}
