using System.Diagnostics;
using System.Globalization;

using Wolverine;

namespace Altinn.Notifications.Shared.Wolverine.Middleware;

/// <summary>
/// Wolverine middleware that measures handler execution time and writes timing metadata to the current activity.
/// </summary>
public static class WolverineHandlerTimingMiddleware
{
    private const string _startedAtHeader = "wolverine-handler-started-at";

    /// <summary>
    /// Captures handler start timestamp in envelope headers.
    /// </summary>
    /// <param name="envelope">The current Wolverine envelope.</param>
    public static void Before(Envelope envelope)
    {
        envelope.Headers[_startedAtHeader] = Stopwatch.GetTimestamp().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes handler duration tags to the current activity if a start timestamp exists.
    /// </summary>
    /// <param name="envelope">The current Wolverine envelope.</param>
    public static void After(Envelope envelope)
    {
        Activity? activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        if (!envelope.Headers.TryGetValue(_startedAtHeader, out string? startedAtString)
            || !long.TryParse(startedAtString, NumberStyles.None, CultureInfo.InvariantCulture, out long startedAt))
        {
            return;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
        activity.SetTag("handle.duration.ms", (int)elapsed.TotalMilliseconds);

        envelope.Headers.Remove(_startedAtHeader);
    }
}
