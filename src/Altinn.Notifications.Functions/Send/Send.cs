using System;
using System.Threading.Tasks;

using Altinn.Notifications.Functions.Integrations;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Functions.Send
{
    public class Send
    {
        private readonly INotifications _notifications;
        public Send(INotifications notifications)
        {
            _notifications = notifications;
        }

        [FunctionName("Send")]
        public async Task Run([QueueTrigger("notifications-outbound", Connection = "Platform:StorageQueueConnectionString")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"// Send // Triggered the queue function.");
            await _notifications.TriggerSendTarget(myQueueItem);
        }
    }
}
