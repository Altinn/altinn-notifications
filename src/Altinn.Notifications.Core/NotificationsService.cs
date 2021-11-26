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

        public NotificationsService(INotificationsRepository notificationsRepository)
        {
            _notificationsRepository = notificationsRepository;
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

        public Task Send(string targetId)
        {
            throw new NotImplementedException();
        }
    }
}
