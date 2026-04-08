using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// No-op implementation of <see cref="ISendSmsPublisher"/> used in integration tests
/// where Wolverine/Azure Service Bus is disabled.
/// </summary>
public class SpySendSmsPublisher : ISendSmsPublisher
{
    /// <summary>
    /// Will return null, indicating that the command was not published to a message bus. This allows tests to verify that the command publisher was called without relying on external infrastructure.
    /// </summary>
    /// <param name="sms">The object containing the body of the message</param>
    /// <param name="cancellationToken">The cancellation token used to propagate notification that the operation should be canceled.</param>
    /// <returns></returns>
    Task<Sms?> ISendSmsPublisher.PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        return Task.FromResult<Sms?>(null);
    }

    public Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Sms>>(Array.Empty<Sms>());
    }
}
