using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Wolverine;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Shared.Publishers;

/// <summary>
/// Concrete implementation of <see cref="IWolverinePublisher"/> for Wolverine ASB publishers.
/// Resolves a scoped <see cref="IMessageBus"/> per publish call to avoid capturing a singleton bus.
/// </summary>
public class WolverinePublisher(IServiceProvider serviceProvider) : IWolverinePublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public async Task PublishCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        await SendAsync(command, null);
    }

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    public Task PublishCommandAsync<TCommand>(TCommand command)
        where TCommand : notnull
    {
        return SendAsync(command, null);
    }

    /// <summary>
    /// Sends <paramref name="command"/> to Azure Service Bus with the given <paramref name="options"/>
    /// via a short-lived scoped <see cref="IMessageBus"/>.
    /// </summary>
    public Task PublishCommandAsync<TCommand>(TCommand command, DeliveryOptions options)
        where TCommand : notnull
    {
        return SendAsync(command, options);
    }

    /// <summary>
    /// Sends <paramref name="command"/> through a short-lived scoped <see cref="IMessageBus"/> and
    /// checks whether it was rejected for being too large.
    /// </summary>
    /// <remarks>
    /// The <see cref="OversizedMessageRegistry"/> is checked only after the send completes. The check is skipped when
    /// the registry is not registered.
    /// </remarks>
    /// <typeparam name="TCommand">The type of the command to send.</typeparam>
    /// <param name="command">The command instance. It is also the key used to look up a recorded failure.</param>
    /// <param name="options">
    /// Optional <see cref="DeliveryOptions"/> for the send, such as headers or scheduling.
    /// When <see langword="null"/>, Wolverine's default delivery behavior is used.
    /// </param>
    /// <returns>A task that completes when the command has been sent and the failure check has run.</returns>
    /// <exception cref="ContentTooLargeException">
    /// Thrown when the <see cref="OversizedMessageRegistry"/> has a failure recorded for <paramref name="command"/>.
    /// Reading the failure also removes it from the registry.
    /// </exception>
    private async Task SendAsync<TCommand>(TCommand command, DeliveryOptions? options)
        where TCommand : notnull
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        if (options is null)
        {
            await messageBus.SendAsync(command);
        }
        else
        {
            await messageBus.SendAsync(command, options);
        }

        if (_serviceProvider.GetService<OversizedMessageRegistry>() is { } oversizedMessageRegistry
            && oversizedMessageRegistry.TryTake(command, out var failure))
        {
            throw new ContentTooLargeException(failure.Message, failure);
        }
    }
}
