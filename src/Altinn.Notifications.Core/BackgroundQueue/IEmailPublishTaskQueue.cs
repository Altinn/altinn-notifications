namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Represents a signal for coordinating email publish operations (at most one queued or running).
/// </summary>
public interface IEmailPublishTaskQueue
{
    /// <summary>
    /// Attempts to enqueue a work item.
    /// </summary>
    /// <returns><c>true</c> if a work item was signaled; <c>false</c> if one is already queued.</returns>
    bool TryEnqueue();

    /// <summary>
    /// Waits asynchronously until a work item is signaled.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait if triggered.</param>
    /// <returns>A task that completes when a signal is received.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the wait is canceled via <paramref name="cancellationToken"/>.</exception>
    Task WaitAsync(CancellationToken cancellationToken);
}
