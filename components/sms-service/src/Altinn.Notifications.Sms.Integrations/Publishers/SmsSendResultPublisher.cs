using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="ISmsSendResultDispatcher"/> that dispatches
/// an <see cref="SmsSendResultCommand"/> via Wolverine to publish terminal SMS send operation results.
/// This implementation is active when <c>WolverineSettings:EnableSmsSendResultPublisher</c> is set to <c>true</c>.
/// </summary>
public class SmsSendResultPublisher : ISmsSendResultDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSendResultPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public SmsSendResultPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.SendResult is null)
        {
            throw new InvalidOperationException("Cannot dispatch SMS send result: SendResult is null.");
        }

        if (result.NotificationId is null)
        {
            throw new InvalidOperationException("Cannot dispatch SMS send result: NotificationId is null.");
        }

        if (result.NotificationId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot dispatch SMS send result: NotificationId is empty.");
        }

        var command = new SmsSendResultCommand
        {
            NotificationId = result.NotificationId.Value,
            SendResult = result.SendResult.Value.ToString(),
            GatewayReference = string.IsNullOrWhiteSpace(result.GatewayReference) ? null : result.GatewayReference
        };

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }
}
