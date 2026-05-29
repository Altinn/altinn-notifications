using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Shared.Publishers;

/// <summary>
/// Abstract base class for Wolverine ASB publishers.
/// Resolves a scoped <see cref="IMessageBus"/> per publish call to avoid capturing a singleton bus.
/// </summary>
public abstract class WolverinePublisher(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    protected async Task PublishCommandAsync<TCommand>(TCommand command)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus with the given <paramref name="options"/>
    /// via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    protected async Task PublishCommandAsync<TCommand>(TCommand command, DeliveryOptions options)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command, options);
    }
}
