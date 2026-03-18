using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// No-op implementation of <see cref="IEmailSendPublisher"/> used in integration tests
/// where Wolverine/Azure Service Bus is disabled.
/// </summary>
public class SpyEmailSendPublisher : IEmailSendPublisher
{
    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        return Task.FromResult<Guid?>(null);
    }
}
