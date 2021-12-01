using Altinn.Notifications.Core.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core
{
    public class NotificationsService : INotifications

    {
        private readonly INotificationsRepository _notificationsRepository;
        private readonly IEmail _emailservice;

        private readonly ILogger<INotifications> _logger;

        public NotificationsService(INotificationsRepository notificationsRepository, IEmail emailservice, ILogger<INotifications> logger)
        {
            _notificationsRepository = notificationsRepository;
            _emailservice = emailservice;
            _logger = logger;
        }

        public async Task<Notification> CreateNotification(Notification notification)
        {
           Notification createdNotification = await _notificationsRepository.AddNotification(notification);

            if (notification.Targets.Count > 0)
            {
                foreach (Target target in notification.Targets)
                {
                    target.NotificationId = createdNotification.Id;
                    await _notificationsRepository.AddTarget(target);
                }
            }

            if(notification.Messages.Count > 0)
            {
                foreach (Message message in notification.Messages)
                {
                    message.NotificationId = createdNotification.Id;
                    await _notificationsRepository.AddMessage(message);
                }
            }

           return createdNotification;
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

        public async Task Send(int targetId)
        {
            Target target = await _notificationsRepository.GetTarget(targetId);

            Notification notication = await _notificationsRepository.GetNotification(target.NotificationId);

            Message message = notication.Messages.FirstOrDefault(r => !string.IsNullOrEmpty(r.EmailSubject));

            await _emailservice.SendEmailAsync(target.Address, message.EmailSubject, message.EmailBody);
        }
    }
}
