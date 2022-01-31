using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Tests.Mocks
{
    public class NotificationRepositoryMock : INotificationsRepository
    {
        public Task<Message> AddMessage(Message message)
        {
            return Task.FromResult(message);
        }

        public Task<Notification> AddNotification(Notification notification)
        {
            return Task.FromResult(new Notification
            {
                Id = 1337,
                Sender = notification.Sender,
                SendTime = notification.SendTime,
                InstanceId = notification.InstanceId,
                PartyReference = notification.PartyReference
            });
        }

        public Task<Target> AddTarget(Target target)
        {
            return Task.FromResult(target);
        }

        public Task<Notification?> GetNotification(int id)
        {
            Notification notification = new Notification();
            return Task.FromResult<Notification?>(notification);
        }

        public Task<Target?> GetTarget(int id)
        {
            Target target = new Target();
            return Task.FromResult<Target?>(target);
        }

        public Task<List<Target>> GetUnsentTargets()
        {
            List<Target> targets = new List<Target>();
            targets.Add(new Target() { ChannelType = "Email", Address = "demo@demo.no", Id = 1337 });
            targets.Add(new Target() { ChannelType = "Email", Address = "demo2@demo.no", Id = 1338 });
            targets.Add(new Target() { ChannelType = "Email", Address = "demo3@demo.no", Id = 1339 });
            return Task.FromResult(targets);
        }

        public Task<Target?> UpdateSentTarget(int id)
        {
            switch (id)
            {
                case 1337:
                    return Task.FromResult<Target?>(new Target() { ChannelType = "Email", Address = "demo@demo.no", Id = 1337, Sent = DateTime.UtcNow });
                case 1338:
                    return Task.FromResult<Target?>(new Target() { ChannelType = "Email", Address = "demo@demo.no", Id = 1338, Sent = DateTime.UtcNow });
                case 1339:
                    return Task.FromResult<Target?>(new Target() { ChannelType = "Email", Address = "demo@demo.no", Id = 1339, Sent = DateTime.UtcNow });
            }

            throw new NotImplementedException();
        }
    }
}
