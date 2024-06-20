using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface describing the functionality of the notification schedule service
    /// </summary>
    public interface INotificationScheduleService
    {
        /// <summary>
        /// Checks if currently within send window and can send sms notifications
        /// </summary>
        /// <returns></returns>
       public bool CanSendSmsNotifications();
    }
}
