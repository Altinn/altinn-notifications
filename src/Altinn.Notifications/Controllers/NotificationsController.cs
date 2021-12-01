using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Interfaces.Models;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Altinn.Notifications.Controllers
{
    [Route("notifications/api/v1/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {

        private readonly INotifications _notificationsService;
        private readonly ILogger<NotificationsController> _logger;

        private readonly DateTime defaultSendTime = new DateTime(2000, 1, 1);

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="notificationService">The Notification core service</param>
        public NotificationsController(INotifications notificationService, ILogger<NotificationsController> logger)
        {
            _notificationsService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Operation to 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<NotificationExt> Get(int id)
        {
            Notification notification = await _notificationsService.GetNotification(id);
            return GetNotification(notification);
        }

        // POST api/<ValuesController>
        [HttpPost]
        public async Task<ObjectResult> Post([FromBody] NotificationExt notificationExt)
        {
            Notification notification = await _notificationsService.CreateNotification(GetNotification(notificationExt));
            return Created($"/notifications/api/v1/notifcations/{notification.Id}", notification);
        }


        private Notification GetNotification(NotificationExt notificationExt)
        {
            Notification notification = new Notification()
            {
                InstanceId = notificationExt.InstanceId,
                Messages = GetMessages(notificationExt.Messages),
                Targets = GetTargets(notificationExt.Targets),
                SendTime = defaultSendTime
            };

            return notification;
        }

        private List<Message> GetMessages(List<MessageExt> messagesExt)
        {
            List<Message> messages = new List<Message>();

            if (messagesExt != null)
            {
                foreach (MessageExt messageExt in messagesExt)
                {
                    messages.Add(GetMessage(messageExt));
                }
            }

            return messages;
        }

        private Message GetMessage(MessageExt messageExt)
        {
            Message message = new Message()
            {
                EmailBody = messageExt.EmailBody,
                EmailSubject = messageExt.EmailSubject,
                SmsText = messageExt.SmsText,
                Language = messageExt.Langauge
            };

            return message;
        }

        private List<Target> GetTargets(List<TargetExt> targetsExt)
        {
            List<Target> targets = new List<Target>();
            if (targetsExt != null)
            {
                foreach (TargetExt targetExt in targetsExt)
                {
                    targets.Add(GetTarget(targetExt));
                }
            }

            return targets;
        }

        private Target GetTarget(TargetExt targetExt)
        {
            Target target = new Target()
            {
                Address = targetExt.Address,
                ChannelType = targetExt.ChannelType
            };

            return target;
        }


        private NotificationExt GetNotification(Notification notification)
        {
            NotificationExt notificationExt = new NotificationExt()
            {
                InstanceId = notification.InstanceId,
                Messages = GetMessages(notification.Messages),
                Targets = GetTargets(notification.Targets)
            };

            return notificationExt;
        }


        private List<TargetExt> GetTargets(List<Target> targets)
        {
            List<TargetExt> targetsExts = new List<TargetExt>();

            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    targetsExts.Add(GetTarget(target));
                }
            }

            return targetsExts;
        }

        private TargetExt GetTarget(Target target)
        {
            TargetExt targetExt = new TargetExt()
            {
                Address = target.Address,
                ChannelType = target.ChannelType,
                Failed = target.Failed,
                FailedReason = target.FailedReason,
                Id = target.Id,
                NotificationId = target.NotificationId
            };

            return targetExt;
        }

        private List<MessageExt> GetMessages(List<Message> messages)
        {
            List<MessageExt> messagesExt = new List<MessageExt>();

            if (messages != null)
            {
                foreach (Message message in messages)
                {
                    messagesExt.Add(GetMessage(message));
                }
            }

            return messagesExt;
        }

        private MessageExt GetMessage(Message message)
        {
            MessageExt messageExt = new MessageExt()
            {
                EmailBody = message.EmailBody,
                EmailSubject = message.EmailSubject,
                SmsText = message.SmsText,
                Langauge = message.Language,
            };

            return messageExt;
        }

    }
}
