using System.Collections.Generic;
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
