namespace Altinn.Notifications.Shared.Publishers;

/// <summary>
/// Defines a contract for publishing messages to a message bus.
/// </summary>
public interface IWolverinePublisher
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
}
