using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public interface IToken
    {
        Task<string> GeneratePlatformToken();
    }
}
