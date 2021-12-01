using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public class ScheduleService : ISchedule
    {
        private readonly IQueue _queue;
        private readonly INotifications _outbound;
        private ILogger<ISchedule> _logger;

        public ScheduleService(IQueue queue, INotifications outbound, ILogger<ISchedule> logger)
        {
            _queue = queue;
            _outbound = outbound;
            _logger = logger;
        }

        public async Task QueueOutboundEMail()
        {
            List<int> outboundEmails = await _outbound.GetOutboundEmails();
            if (outboundEmails.Count == 0)
            {
                return;
            }

            _logger.LogInformation($" // ScheduleService // QueueOutboundEMail // Attempting to queue outbound emails");

            foreach (int outboundElement in outboundEmails)
            {
                await _queue.Push(outboundElement.ToString());
            }
        }

        public async Task QueueOutboundSMS()
        {
            List<int> outboundSMS = await _outbound.GetOutboundSMS();
            if (outboundSMS.Count == 0)
            {
                return;
            }

            _logger.LogInformation($" // ScheduleService // QueueOutboundSMS // Attempting to queue outbound sms");

            foreach (int outboundElement in outboundSMS)
            {
                await _queue.Push(outboundElement.ToString());
            }
        }
    }
}
