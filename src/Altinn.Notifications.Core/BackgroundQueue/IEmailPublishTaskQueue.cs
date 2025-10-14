namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Represents a queue for coordingating email publish operations.
/// </summary>
public interface IEmailPublishTaskQueue
{
    /// <summary>
    /// Marks the current work item for the specified sending time policy as completed, allowing new work items to be scheduled.
    /// </summary>
    void MarkCompleted();

    /// <summary>
    /// Waits asynchronously until a work item is signaled.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait if triggered.</param>
    /// <returns>A task that completed when a signal is received.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the wait is canceled via <paramref name="cancellationToken"/>.</exception>
    Task WaitAsync(CancellationToken cancellationToken);
}
