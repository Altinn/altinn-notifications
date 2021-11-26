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

        public void CreateNotification(Notification notification);

        public List<string> GetSmsTarget();

        public List<string> GetEmailTarget();

        public void Send(string targetId);

        public Notification GetNotification(int notificationId);
    }
}
