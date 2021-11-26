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
        private readonly INotificationsRepository _notificationsRepository;
        private readonly IEmail _emailservice;

        public NotificationsService(INotificationsRepository notificationsRepository, IEmail emailservice)
        {
            _notificationsRepository = notificationsRepository;
            _emailservice = emailservice;
        }

        public async Task<Notification> CreateNotification(Notification notification)
        {
           return await _notificationsRepository.AddNotification(notification);
        }

        public async Task<List<Target>> GetUnsentEmailTargets()
        {
           return await _notificationsRepository.GetUnsentTargets();
        }

        public async Task<Notification> GetNotification(int notificationId)
        {
            return await _notificationsRepository.GetNotification(notificationId);
        }

        public async Task<List<Target>> GetUnsentSmsTargets()
        {
            return await _notificationsRepository.GetUnsentTargets();
        }

        public async Task Send(string targetId)
        {
            Target target = new Target();

            Notification notication = await _notificationsRepository.GetNotification(target.NotificationId);

            Message message = notication.Messages.FirstOrDefault(r => !string.IsNullOrEmpty(r.EmailSubject));

            await _emailservice.SendEmailAsync(target.Address, message.EmailSubject, message.EmailBody);
        }
    }
}
