using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Represents a queue for coordinating SMS publish operations per <see cref="SendingTimePolicy"/>.
/// At most one work item per policy can be active or pending at any given time.
/// </summary>
public interface ISmsPublishTaskQueue
{
    /// <summary>
    /// Attempts to enqueue a work item for the specified sending time policy.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy for which the task should be enqueued.</param>
    /// <returns>
    /// <c>true</c> if the task was successfully enqueued; <c>false</c> if a task for the given policy is already queued or running.
    /// </returns>
    bool TryEnqueue(SendingTimePolicy sendingTimePolicy);

    /// <summary>
    /// Marks the current work item for the specified sending time policy as completed, allowing new work items for that policy to be scheduled.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy to release.</param>
    void MarkCompleted(SendingTimePolicy sendingTimePolicy);

    /// <summary>
    /// Waits asynchronously until a work item is signaled for the specified sending time policy.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy to wait on.</param>
    /// <param name="cancellationToken">A token that cancels the wait if triggered.</param>
    /// <returns>A task that completes when a signal for the specified policy is received.</returns>
    /// <exception cref="OperationCanceledException"> Thrown if the wait is canceled via <paramref name="cancellationToken"/>.</exception>
    Task WaitAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken);
}
