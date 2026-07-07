namespace Altinn.Notifications.Tools.PerfSnapshot.Configuration;

/// <summary>
/// Settings controlling what the perf snapshot tool polls and how often.
/// </summary>
public class PerfSnapshotSettings
{
    /// <summary>
    /// The sendersReference shared by all orders in the performance test run to monitor.
    /// </summary>
    public string SendersReference { get; set; } = string.Empty;

    /// <summary>
    /// How often, in seconds, to poll the database for a new snapshot.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Path (relative or absolute) of the CSV file snapshots are appended to.
    /// </summary>
    public string OutputFilePath { get; set; } = "perf-snapshot.csv";

    /// <summary>
    /// Optional safety bound: stop polling automatically after this many minutes.
    /// Leave unset (or 0) to run until manually stopped with Ctrl+C.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Per-query command timeout, in seconds. These queries scan large tables without a
    /// covering index on sendersreference, so they can take well over a minute once a
    /// performance test has grown the table — the default Npgsql timeout (30s) is too
    /// short and would report every snapshot as failed. Defaults to 5 minutes.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;
}
