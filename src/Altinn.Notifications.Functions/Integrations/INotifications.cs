using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public interface INotifications
    {
        public Task<List<int>> GetOutboundSMS();

        public Task<List<int>> GetOutboundEmails();

        public Task TriggerSendTarget(string targetId);
    }
}
