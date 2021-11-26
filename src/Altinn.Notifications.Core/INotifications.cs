using Altinn.Notifications.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core
{
    public  interface INotifications
    {

        public Task<Notification> CreateNotification(Notification notification);

        public Task<List<string>> GetSmsTarget();

        public Task<List<string>> GetEmailTarget();

        public Task Send(string targetId);

        public Task<Notification> GetNotification(int notificationId);
    }
}
