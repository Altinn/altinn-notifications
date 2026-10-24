namespace Altinn.Notifications.Tools.Simulator.Configuration;

/// <summary>
/// Settings for the scheduler that calls the API's trigger endpoints, the way the cluster's cron jobs do.
/// </summary>
public class TriggerSettings
{
    /// <summary>
    /// Whether the jobs run on their own. When <c>false</c> every job starts paused, and can still be run by hand or resumed in the GUI.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The API the triggers are sent to.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5090";

    /// <summary>
    /// How long a trigger call may take before it counts as failed.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// How many runs each job keeps in its history.
    /// </summary>
    public int HistorySize { get; set; } = 20;

    /// <summary>
    /// Overrides per job, keyed by the trigger name, e.g. <c>sendemail</c>. Jobs that are left out keep their defaults.
    /// </summary>
    public Dictionary<string, TriggerJobSettings> Jobs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Overrides for one trigger job. What is left out keeps the job's default.
/// </summary>
public class TriggerJobSettings
{
    /// <summary>
    /// Whether the job runs on its own. A paused job can still be run by hand.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Seconds between runs.
    /// </summary>
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// The UTC hours the job runs in, like the hour field of a cron expression, e.g. <c>7-16</c>. Empty means all day.
    /// </summary>
    public string? Hours { get; set; }
}
