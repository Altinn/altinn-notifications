using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Tests.Mocks
{
    public class NotificationRepositoryMock : INotificationsRepository
    {
        public Task<Message> AddMessage(Message message)
        {
            throw new NotImplementedException();
        }

        public Task<Notification> AddNotification(Notification notification)
        {
            notification.Id = 1337;
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
            throw new NotImplementedException();
        }
    }
}
