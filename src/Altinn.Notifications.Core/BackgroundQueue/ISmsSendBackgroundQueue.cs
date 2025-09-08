using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Queue for scheduling SMS send operations per <see cref="SendingTimePolicy"/>.
/// </summary>
public interface ISmsSendBackgroundQueue
{
    /// <summary>
    /// Attempts to enqueue a work item for processing SMS notifications associated with the specified sending time policy.
    /// </summary>
    /// <param name="sendingTimePolicy">
    /// The <see cref="SendingTimePolicy"/> to identify which SMS notifications should be processed.
    /// </param>
    /// <returns>
    /// <c>true</c> if a new work item was scheduled; <c>false</c> if the request was coalesced because work for the same policy
    /// is already queued or in progress.
    /// </returns>
    bool TryEnqueue(SendingTimePolicy sendingTimePolicy);

    /// <summary>
    /// Marks completion of a work item used to process SMS notifications associated with the specified sending time policy.
    /// </summary>
    void MarkCompleted(SendingTimePolicy sendingTimePolicy);

    /// <summary>
    /// Asynchronously dequeues the next work item for processing SMS notifications associated with the specified sending time policy.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the processing.</param>
    /// <returns>The next scheduled work item for the given <see cref="SendingTimePolicy"/>.</returns>
    Task<SendingTimePolicy> DequeueAsync(CancellationToken cancellationToken);
}
