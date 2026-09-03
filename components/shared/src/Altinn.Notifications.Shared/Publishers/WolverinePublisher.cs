using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Altinn.Notifications.Shared.Publishers;

/// <summary>
/// Concrete implementation of <see cref="IMessageBusPublisher"/> for Wolverine ASB publishers.
/// Resolves a scoped <see cref="IMessageBus"/> per publish call to avoid capturing a singleton bus.
/// </summary>
public class WolverinePublisher(IServiceProvider serviceProvider) : IMessageBusPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TItem>> PublishBatchAsync<TItem, TCommand>(IReadOnlyList<TItem> items, Func<TItem, TCommand> commandFactory, Action<TItem, Exception>? onError, CancellationToken cancellationToken)
        where TCommand : notnull
    {
        if (items.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var failed = new ConcurrentBag<TItem>();

        await Task.WhenAll(items.Select(async item =>
        {
            try
            {
                await messageBus.SendAsync(commandFactory(item));
            }
            catch (Exception ex)
            {
                failed.Add(item);
                onError?.Invoke(item, ex);
            }
        }));

        return [.. failed];
    }

    /// <inheritdoc/>
    public async Task PublishCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    public async Task PublishCommandAsync<TCommand>(TCommand command)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus with the given <paramref name="options"/>
    /// via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    public async Task PublishCommandAsync<TCommand>(TCommand command, DeliveryOptions options)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command, options);
    }
}
