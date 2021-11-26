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

        async Task ISchedule.QueueOutboundEMail()
        {
            _logger.LogInformation("// Schedule // Retreive outbound emails");
            List<int> outboundEmails = await _outbound.GetOutboundEmails();
            if (outboundEmails.Count == 0)
            {
                _logger.LogInformation("// Schedule // No elements returned. ");
                return;
            }

            _logger.LogInformation($"// Schedule // Retrieved {outboundEmails.Count} pending emails.");

            foreach (int outboundElement in outboundEmails)
            {
                await _queue.Push(outboundElement.ToString());
            }
        }

        async Task ISchedule.QueueOutboundSMS()
        {
            _logger.LogInformation("Trying to retreive outbound sms");

            List<int> outboundSMS = await _outbound.GetOutboundSMS();
            if (outboundSMS.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Pushing outbound sms to queue.");

            foreach (int outboundElement in outboundSMS)
            {
                await _queue.Push(outboundElement.ToString());
            }
        }
    }
}
