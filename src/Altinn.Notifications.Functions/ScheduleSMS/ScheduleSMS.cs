using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Functions.Integrations;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Functions
{
    public class ScheduleSMS
    {
        private readonly ISchedule _schedule;

        public ScheduleSMS(ISchedule schedule)
        {
            _schedule = schedule;
        }

        [FunctionName("ScheduleSMS")]
        public async Task Run([TimerTrigger("* */10 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Trigger for sending SMS executed at: {DateTime.Now}");

            await _schedule.QueueOutboundSMS();
        }
    }
}
