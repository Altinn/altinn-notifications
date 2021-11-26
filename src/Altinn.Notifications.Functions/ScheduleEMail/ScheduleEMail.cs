using System;
using System.Threading.Tasks;

using Altinn.Notifications.Functions.Integrations;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Functions
{
    public class ScheduleEMail
    {
        private readonly ISchedule _schedule;

        public ScheduleEMail(ISchedule schedule)
        {
            _schedule = schedule;
        }

        [FunctionName("ScheduleEMail")]
        public async Task Run([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($" // Shedule emil // Trigger for sending emails executed at: {DateTime.Now}");

            await _schedule.QueueOutboundEMail();
            log.LogInformation($" // Shedule emil // Completed");

        }
    }
}
