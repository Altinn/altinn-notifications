namespace Altinn.Notifications.Tools.TestHarness;

/// <summary>
/// A request a mock forwarded, as it arrived.
/// </summary>
internal sealed record SimulatedRequest(
    string Id,
    string System,
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string Body,
    DateTimeOffset ReceivedAt,
    DateTimeOffset ExpiresAt,
    string? TraceId);

/// <summary>
/// The answer that goes back to the mock, and through it to the caller.
/// </summary>
internal sealed record SimulatedResponse(int Status, IReadOnlyDictionary<string, string> Headers, string Body)
{
    public static SimulatedResponse Timeout(int seconds) => new(
        StatusCodes.Status504GatewayTimeout,
        new Dictionary<string, string> { ["Content-Type"] = "application/json", [SimulatorHeader] = "timeout" },
        $$"""{"error":"No answer from the simulator within {{seconds}} seconds."}""");

    /// <summary>
    /// Tells the caller where an answer came from: <c>operator</c>, <c>auto</c> or <c>timeout</c>.
    /// </summary>
    public const string SimulatorHeader = "X-Simulator";
}

/// <summary>
/// How a held request ended.
/// </summary>
internal enum Outcome
{
    /// <summary>The operator answered it in the GUI.</summary>
    Answered,

    /// <summary>An auto-reply template answered it at once.</summary>
    AutoReplied,

    /// <summary>Nobody answered within the timeout, so it got a 504.</summary>
    TimedOut,

    /// <summary>The caller gave up before anyone answered.</summary>
    Cancelled,
}

/// <summary>
/// A handled request in the history. <see cref="Response"/> is null when the caller gave up.
/// </summary>
internal sealed record HistoryEntry(SimulatedRequest Request, SimulatedResponse? Response, Outcome Outcome, DateTimeOffset HandledAt);
