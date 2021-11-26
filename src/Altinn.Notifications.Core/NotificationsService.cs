using Altinn.Notifications.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core
{
    internal class NotificationsService : INotifications
    {
        public Task<Notification> CreateNotification(Notification notification)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetEmailTarget()
        {
            throw new NotImplementedException();
        }

        public Task<Notification> GetNotification(int notificationId)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetSmsTarget()
        {
            throw new NotImplementedException();
        }

        public Task Send(string targetId)
        {
            throw new NotImplementedException();
        }
    }
}
