using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public interface ISchedule
    {
        public Task QueueOutboundSMS();

        public Task QueueOutboundEMail();
    }
}
