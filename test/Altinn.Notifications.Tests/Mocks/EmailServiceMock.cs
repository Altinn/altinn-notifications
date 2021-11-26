using Altinn.Notifications.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
