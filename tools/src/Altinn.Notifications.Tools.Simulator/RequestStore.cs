using System.Collections.Concurrent;

using Altinn.Notifications.Tools.Simulator.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.Simulator;

/// <summary>
/// The requests waiting for an answer, the ones already handled, and the auto-reply template of each system.
/// </summary>
internal sealed class RequestStore(IOptions<SimulatorSettings> options)
{
    private readonly ConcurrentDictionary<string, HeldRequest> _pending = new();
    private readonly ConcurrentDictionary<string, string> _autoReplies = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<HistoryEntry> _history = new();

    /// <summary>
    /// Raised whenever a request arrives or is handled, or an auto-reply changes, so the GUI can refresh.
    /// </summary>
    public event Action? Changed;

    public IReadOnlyList<SimulatedRequest> Pending => [.. _pending.Values.Select(p => p.Request).OrderBy(r => r.ReceivedAt)];

    public IReadOnlyList<HistoryEntry> History
    {
        get
        {
            lock (_history)
            {
                return [.. _history];
            }
        }
    }

    public IReadOnlyDictionary<string, string> AutoReplies => new Dictionary<string, string>(_autoReplies);

    /// <summary>
    /// Holds <paramref name="request"/> until <see cref="TryResolve"/> is called for it. The task completes with the answer.
    /// </summary>
    public Task<SimulatedResponse> Hold(SimulatedRequest request)
    {
        var pending = new HeldRequest(request, new TaskCompletionSource<SimulatedResponse>(TaskCreationOptions.RunContinuationsAsynchronously));
        _pending[request.Id] = pending;
        Changed?.Invoke();
        return pending.Answer.Task;
    }

    /// <summary>
    /// Answers a held request and moves it to the history. Only the first of an answer and a timeout wins.
    /// </summary>
    /// <returns><c>false</c> if the request is not held, e.g. because it already timed out.</returns>
    public bool TryResolve(string id, SimulatedResponse? response, Outcome outcome, DateTimeOffset now)
    {
        if (!_pending.TryRemove(id, out var pending))
        {
            return false;
        }

        AddHistory(new HistoryEntry(pending.Request, response, outcome, now));
        pending.Answer.TrySetResult(response ?? SimulatedResponse.Timeout(0));
        return true;
    }

    public void AddHistory(HistoryEntry entry)
    {
        lock (_history)
        {
            _history.AddFirst(entry);
            while (_history.Count > Math.Max(options.Value.HistorySize, 1))
            {
                _history.RemoveLast();
            }
        }

        Changed?.Invoke();
    }

    public string? GetAutoReply(string system) => _autoReplies.TryGetValue(system, out string? template) ? template : null;

    /// <summary>
    /// Answers every request to <paramref name="system"/> with <paramref name="template"/> at once, or holds them again when it is null.
    /// </summary>
    public void SetAutoReply(string system, string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            _autoReplies.TryRemove(system, out _);
        }
        else
        {
            _autoReplies[system] = template;
        }

        Changed?.Invoke();
    }

    private sealed record HeldRequest(SimulatedRequest Request, TaskCompletionSource<SimulatedResponse> Answer);
}
