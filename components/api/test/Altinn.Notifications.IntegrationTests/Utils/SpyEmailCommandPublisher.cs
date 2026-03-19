using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// No-op implementation of <see cref="IEmailCommandPublisher"/> used in integration tests
/// where Wolverine/Azure Service Bus is disabled.
/// </summary>
public class SpyEmailCommandPublisher : IEmailCommandPublisher
{
    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        return Task.FromResult<Guid?>(null);
    }
}
