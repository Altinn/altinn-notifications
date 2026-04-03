using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// No-op implementation of <see cref="ISendSmsCommandPublisher"/> used in integration tests
/// where Wolverine/Azure Service Bus is disabled.
/// </summary>
public class SpySendSmsPublisher : ISendSmsCommandPublisher
{
    /// <summary>
    /// Will return null, indicating that the command was not published to a message bus. This allows tests to verify that the command publisher was called without relying on external infrastructure.
    /// </summary>
    /// <param name="sms">The object containing the body of the message</param>
    /// <param name="cancellationToken">The cancellation token used to propagate notification that the operation should be canceled.</param>
    /// <returns></returns>
    public Task<Guid?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
       return Task.FromResult<Guid?>(null);
    }
}
