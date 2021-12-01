using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public interface INotifications
    {
        public Task<List<string>> GetOutboundSMS();

        public Task<List<string>> GetOutboundEmails();

        public Task TriggerSendTarget(string targetId);
    }
}
