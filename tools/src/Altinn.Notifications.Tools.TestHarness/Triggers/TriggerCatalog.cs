namespace Altinn.Notifications.Tools.TestHarness.Triggers;

/// <summary>
/// One endpoint in the API's <c>TriggerController</c>, with the defaults the local scheduler uses for it.
/// </summary>
/// <param name="Name">The last segment of <c>notifications/api/v1/trigger/{name}</c>.</param>
/// <param name="Description">What the API does when it is called.</param>
/// <param name="ClusterSchedule">The cron expression of the job in the cluster, or <c>null</c> when there is none.</param>
/// <param name="Enabled">Whether the job runs on its own by default.</param>
/// <param name="IntervalSeconds">Seconds between runs by default. Shorter than in the cluster, so a local order goes out quickly.</param>
/// <param name="Hours">The UTC hours the job runs in by default, taken from the cluster's cron expression.</param>
internal sealed record TriggerDefinition(string Name, string Description, string? ClusterSchedule, bool Enabled, int IntervalSeconds, string Hours);

/// <summary>
/// The trigger endpoints. The cluster schedules are from docs/architecture/notifications.md.
/// </summary>
internal static class TriggerCatalog
{
    public const string TriggerPath = "notifications/api/v1/trigger/";

    public static readonly IReadOnlyList<TriggerDefinition> All =
    [
        new("sendemail", "Sends waiting email notifications with the anytime policy, and composed ones.", "*/1 * * * *", true, 15, string.Empty),
        new("sendemaildaytime", "Sends waiting email notifications with the daytime policy. The API skips it outside its email send window.", null, true, 15, string.Empty),
        new("sendsmsanytime", "Sends waiting SMS notifications with the anytime policy.", "*/1 * * * *", true, 15, string.Empty),
        new("sendsms", "Sends waiting SMS notifications with the daytime policy (also routed as sendsmsdaytime). The API skips it outside its SMS send window.", "* 7-16 * * *", true, 15, "7-16"),
        new("terminateexpirednotifications", "Marks notifications past their expiry as failed and completes their orders.", "*/15 * * * *", true, 60, string.Empty),
        new("deleteoldstatusfeedrecords", "Deletes status feed records older than 90 days.", "*/5 2-5 * * *", false, 300, "2-5"),
        new("pastdueoneorder", "Processes one past-due order. Debug only: the API's own background service already does this.", null, false, 10, string.Empty),
        new("retryoneorder", "Retries one order. Debug only: the API's own background service already does this.", null, false, 10, string.Empty),
    ];
}
