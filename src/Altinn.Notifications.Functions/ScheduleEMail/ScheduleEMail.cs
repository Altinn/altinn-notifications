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
        public async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            await _schedule.QueueOutboundEMail();
        }
    }
}
