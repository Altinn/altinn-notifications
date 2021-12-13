using Altinn.Notifications.Core.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
                createdNotification.Targets = new List<Target>();
                
                foreach (Target target in notification.Targets)
                {
                    target.NotificationId = createdNotification.Id;
                    Target createdTarget = await _notificationsRepository.AddTarget(target);
                    createdNotification.Targets.Add(createdTarget);
                }
            }

            if(notification.Messages.Count > 0)
            {
                createdNotification.Messages = new List<Message>();

                foreach (Message message in notification.Messages)
                {
                    message.NotificationId = createdNotification.Id;
                    Message createdMessage = await _notificationsRepository.AddMessage(message);
                    createdNotification.Messages.Add(createdMessage);
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

            Notification notification = await _notificationsRepository.GetNotification(target.NotificationId);

            Message message = notification.Messages.First(r => !string.IsNullOrEmpty(r.EmailSubject));

            await _emailservice.SendEmailAsync(target.Address, message.EmailSubject, message.EmailBody);

            await _notificationsRepository.UpdateSentTarget(target.Id);
        }
    }
}
