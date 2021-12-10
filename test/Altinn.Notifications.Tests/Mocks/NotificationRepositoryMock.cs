using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Tests.Mocks
{
    public class NotificationRepositoryMock : INotificationsRepository
    {
        public Task<Message> AddMessage(Message message)
        {
            return Task.FromResult(message);
        }

        public Task<Notification> AddNotification(Notification notification)
        {
            notification.Id = 1337;
            notification.Targets = null;
            notification.Messages = null;
            return Task.FromResult(notification);
        }

        public Task<Target> AddTarget(Target target)
        {
            return  Task.FromResult(target);
        }

        public Task<Notification> GetNotification(int id)
        {
            Notification notification = new Notification();
            return Task.FromResult(notification);
        }

        public Task<Target> GetTarget(int id)
        {
            Target target = new Target();
            return Task.FromResult(target);
        }

        public Task<List<Target>> GetUnsentTargets()
        {
            List<Target> targets = new List<Target>();
            targets.Add(new Target() { ChannelType = "Email", Address = "demo@demo.no", Id = 1337 });
            targets.Add(new Target() { ChannelType = "Email", Address = "demo2@demo.no", Id = 1338 });
            targets.Add(new Target() { ChannelType = "Email", Address = "demo3@demo.no", Id = 1339 });
            return Task.FromResult(targets);
        }
    }
}
