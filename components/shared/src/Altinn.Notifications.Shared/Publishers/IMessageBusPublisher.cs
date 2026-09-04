namespace Altinn.Notifications.Shared.Publishers;

/// <summary>
/// Defines a contract for publishing messages to a message bus.
/// </summary>
public interface IMessageBusPublisher
{
    /// <summary>
    /// Publishes a command to the message bus.
    /// </summary>
    /// <typeparam name="TCommand">The command to publish.</typeparam>
    /// <param name="command">The command instance to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    Task PublishCommandAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : notnull;

    /// <summary>
    /// Publishes a batch of commands to the message bus with a specified concurrency level.
    /// </summary>
    /// <typeparam name="TItem">The type of the items to publish.</typeparam>
    /// <typeparam name="TCommand">The type of the command to publish.</typeparam>
    /// <param name="items">The items to publish.</param>
    /// <param name="commandFactory">A factory function to create a command from an item.</param>
    /// <param name="onError">An optional callback invoked for each item that fails to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous publish operation, containing the items that failed to publish.</returns>
    Task<IReadOnlyList<TItem>> PublishBatchAsync<TItem, TCommand>(
        IReadOnlyList<TItem> items,
        Func<TItem, TCommand> commandFactory,
        Action<TItem, Exception>? onError,
        CancellationToken cancellationToken)
        where TCommand : notnull;
}
