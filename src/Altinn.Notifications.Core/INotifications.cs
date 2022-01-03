using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core
{
    public  interface INotifications
    {

        public Task<Notification> CreateNotification(Notification notification);

        public Task<List<Target>> GetUnsentSmsTargets();

        public Task<List<Target>> GetUnsentEmailTargets();

        public Task Send(int targetId);

        public Task<Notification?> GetNotification(int notificationId);
    }
}
