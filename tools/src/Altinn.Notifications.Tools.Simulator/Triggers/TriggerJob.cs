namespace Altinn.Notifications.Tools.Simulator.Triggers;

/// <summary>
/// One call to a trigger endpoint.
/// </summary>
/// <param name="StartedAt">When the call was sent.</param>
/// <param name="DurationMs">How long the API took to answer.</param>
/// <param name="Source"><c>scheduled</c> or <c>manual</c>.</param>
/// <param name="Status">The HTTP status, or <c>null</c> when the API could not be reached.</param>
/// <param name="Response">The start of the response body, or the error when there was no response.</param>
internal sealed record TriggerRun(DateTimeOffset StartedAt, long DurationMs, string Source, int? Status, string Response)
{
    public bool Succeeded => Status is >= 200 and < 300;
}

/// <summary>
/// A trigger endpoint, whether it runs on its own and how often, and its last runs.
/// </summary>
internal sealed class TriggerJob
{
    private readonly object _lock = new();
    private readonly LinkedList<TriggerRun> _history = new();
    private readonly int _historySize;
    private CancellationTokenSource _wake = new();

    public TriggerJob(TriggerDefinition definition, bool enabled, int intervalSeconds, bool[] hours, int historySize)
    {
        Definition = definition;
        Enabled = enabled;
        IntervalSeconds = intervalSeconds;
        Hours = hours;
        _historySize = Math.Max(historySize, 1);
    }

    public TriggerDefinition Definition { get; }

    public string Name => Definition.Name;

    public bool Enabled { get; private set; }

    public int IntervalSeconds { get; private set; }

    /// <summary>
    /// For each UTC hour, whether the job runs in it.
    /// </summary>
    public bool[] Hours { get; private set; }

    /// <summary>
    /// Held while a call is in flight, so a job never overlaps itself, like <c>concurrencyPolicy: Forbid</c> in the cluster.
    /// </summary>
    public SemaphoreSlim Running { get; } = new(1, 1);

    public DateTimeOffset? NextRunAt { get; set; }

    public IReadOnlyList<TriggerRun> History
    {
        get
        {
            lock (_lock)
            {
                return [.. _history];
            }
        }
    }

    /// <summary>
    /// Cancelled when the settings change, so the scheduler stops waiting and works out the next run again.
    /// </summary>
    public CancellationToken WakeToken
    {
        get
        {
            lock (_lock)
            {
                return _wake.Token;
            }
        }
    }

    public bool RunsAt(DateTimeOffset now) => Hours[now.UtcDateTime.Hour];

    public void Update(bool? enabled, int? intervalSeconds, bool[]? hours)
    {
        CancellationTokenSource previous;
        lock (_lock)
        {
            Enabled = enabled ?? Enabled;
            IntervalSeconds = intervalSeconds ?? IntervalSeconds;
            Hours = hours ?? Hours;
            previous = _wake;
            _wake = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    public void AddRun(TriggerRun run)
    {
        lock (_lock)
        {
            _history.AddFirst(run);
            while (_history.Count > _historySize)
            {
                _history.RemoveLast();
            }
        }
    }
}

/// <summary>
/// The hour field of a cron expression: <c>7-16</c>, <c>3</c>, <c>22-2</c> or a comma-separated list of those. Empty or <c>*</c> is every hour.
/// </summary>
internal static class HourRange
{
    public static bool TryParse(string? text, out bool[] hours)
    {
        hours = new bool[24];
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "*")
        {
            Array.Fill(hours, true);
            return true;
        }

        foreach (string part in text.Split(',', StringSplitOptions.TrimEntries))
        {
            string[] ends = part.Split('-', StringSplitOptions.TrimEntries);
            if (ends.Length > 2 || !int.TryParse(ends[0], out int from) || from is < 0 or > 23)
            {
                return false;
            }

            int to = from;
            if (ends.Length == 2 && (!int.TryParse(ends[1], out to) || to is < 0 or > 23))
            {
                return false;
            }

            // 22-2 wraps past midnight.
            for (int hour = from; ; hour = (hour + 1) % 24)
            {
                hours[hour] = true;
                if (hour == to)
                {
                    break;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The hours back as text, e.g. <c>7-16</c>, or an empty string for all day.
    /// </summary>
    public static string Format(bool[] hours)
    {
        if (hours.All(h => h))
        {
            return string.Empty;
        }

        List<string> parts = [];
        for (int hour = 0; hour < 24; hour++)
        {
            if (!hours[hour])
            {
                continue;
            }

            int end = hour;
            while (end < 23 && hours[end + 1])
            {
                end++;
            }

            parts.Add(end == hour ? $"{hour}" : $"{hour}-{end}");
            hour = end;
        }

        return string.Join(',', parts);
    }
}
