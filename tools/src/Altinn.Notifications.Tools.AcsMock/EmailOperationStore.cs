using System.Collections.Concurrent;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// A send the mock has accepted, as returned by <c>GET /emails/operations/{id}</c>.
/// </summary>
internal sealed class EmailOperation
{
    private int _reportQueued;
    private int _failureMirrored;

    public required string Id { get; init; }

    public required EmailScenario Scenario { get; init; }

    public required string Sender { get; init; }

    public required IReadOnlyList<string> Recipients { get; init; }

    public required DateTimeOffset CompletesAt { get; init; }

    public string? TraceParent { get; init; }

    public string? TraceState { get; init; }

    /// <summary>
    /// <c>Running</c> until <see cref="CompletesAt"/>, then <c>Succeeded</c> or <c>Failed</c>.
    /// </summary>
    public string Status(DateTimeOffset now)
    {
        if (now < CompletesAt)
        {
            return "Running";
        }

        return Scenario is EmailScenario.Failed or EmailScenario.Suppressed ? "Failed" : "Succeeded";
    }

    /// <summary>
    /// <c>true</c> the first time it is called, so the delivery report is only queued once.
    /// </summary>
    public bool MarkReportQueued() => Interlocked.Exchange(ref _reportQueued, 1) == 0;

    /// <summary>
    /// <c>true</c> the first time it is called, so a failed operation is only copied to the control panel once.
    /// </summary>
    public bool MarkFailureMirrored() => Interlocked.Exchange(ref _failureMirrored, 1) == 0;
}

/// <summary>
/// Holds the operations in memory. They are gone when the mock restarts, and a poll for one of them gets 404.
/// </summary>
internal sealed class EmailOperationStore
{
    private readonly ConcurrentDictionary<string, EmailOperation> _operations = new();
    private readonly ConcurrentDictionary<string, byte> _throttled = new();

    /// <summary>
    /// Adds an operation, or returns the one already stored with the same id, so a retried send is not sent twice.
    /// </summary>
    public EmailOperation GetOrAdd(EmailOperation operation) => _operations.GetOrAdd(operation.Id, operation);

    public EmailOperation? Get(string id) => _operations.TryGetValue(id, out var operation) ? operation : null;

    /// <summary>
    /// <c>true</c> the first time a send with this body hash is seen, so a throttled send gets through on its next attempt.
    /// </summary>
    public bool IsFirstAttempt(string contentHash) => _throttled.TryAdd(contentHash, 0);
}
