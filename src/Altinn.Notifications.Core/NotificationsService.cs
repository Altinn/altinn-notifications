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
        public Notification CreateNotification(Notification notification)
        {
            throw new NotImplementedException();
        }

        public List<string> GetEmailTarget()
        {
            throw new NotImplementedException();
        }

        public Notification GetNotification(int notificationId)
        {
            throw new NotImplementedException();
        }

        public List<string> GetSmsTarget()
        {
            throw new NotImplementedException();
        }

        public void Send(string targetId)
        {
            throw new NotImplementedException();
        }
    }
}
