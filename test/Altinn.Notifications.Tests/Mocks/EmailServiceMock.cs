using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core;

namespace Altinn.Notifications.Tests.Mocks
{
    public class EmailServiceMock : IEmail
    {
        public Task<bool> SendEmailAsync(string address, string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
