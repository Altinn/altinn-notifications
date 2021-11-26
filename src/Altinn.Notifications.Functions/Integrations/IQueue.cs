using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public interface IQueue
    {
        public Task Push(string message);
    }
}
