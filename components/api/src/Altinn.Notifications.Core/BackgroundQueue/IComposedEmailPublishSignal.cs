namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Defines a coordination mechanism for composed email publish operations,
/// ensuring that at most one operation is queued or executing at any given time.
/// </summary>
public interface IComposedEmailPublishSignal
{
    /// <summary>
    /// Signals the queue that a composed email publish operation should be performed.
    /// If an operation is already pending, the request is ignored.
    /// </summary>
    /// <returns><c>true</c> if the signal was accepted; <c>false</c> if an operation is already queued.</returns>
    bool TryEnqueue();

    /// <summary>
    /// Asynchronously waits until a publish operation is signaled.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous wait.</param>
    /// <returns>A <see cref="Task"/> that completes when a signal has been received.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is triggered.</exception>
    Task WaitAsync(CancellationToken cancellationToken);
}
