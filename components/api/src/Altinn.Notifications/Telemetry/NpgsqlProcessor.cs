using System.Diagnostics;
using System.Text;
using Altinn.Notifications.Persistence.Configuration;
using OpenTelemetry;

namespace Altinn.Notifications.Telemetry
{
    /// <summary>
    /// Processes PostgreSQL-related activities to reduce low-value telemetry, aggregate database execution statistics
    /// on the root request activity, and enrich request telemetry with selected user claim tags.
    /// </summary>
    /// <remarks>Short PostgreSQL activities can be marked as not recorded, command parameters are removed for
    /// short operations, and temporary baggage entries are cleared after use to prevent propagation across process
    /// boundaries.</remarks>
    /// <param name="postgreSqlSettings">Postgres config</param>
    public class NpgsqlProcessor(PostgreSqlSettings postgreSqlSettings) : BaseProcessor<Activity>
    {
        private const string DbStatsBaggagePrefix = "npgsql.processor.hash.";

        /// <summary>
        /// Processes completed activities by suppressing low-duration Npgsql spans, sanitizing propagated data, and
        /// aggregating database execution statistics on the root server activity.
        /// </summary>
        /// <remarks>For Npgsql activities, short-duration spans can be marked as not recorded and have
        /// command parameter tags removed. Hash and duration data are transferred through baggage to the root server
        /// activity, where totals and averages are calculated and written to a database statistics tag, then temporary
        /// baggage entries are cleared.</remarks>
        /// <param name="activity">The completed activity to inspect, update, and optionally enrich with aggregated database telemetry.</param>
        public override void OnEnd(Activity activity)
        {
            // Suppress Npgsql activities with short duration if it's not the first time we see this trace
            if (IsNpgsqlActivity(activity))
            {
                var minimumTotalDuration = TimeSpan.FromMilliseconds(postgreSqlSettings.TelemetryMinDurationMs);
                bool isFirstHash = activity.GetCustomProperty("firsthash") is bool firstHash && firstHash;
                if (activity.Duration < minimumTotalDuration && !isFirstHash)
                {
                    // Suppress export
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                }

                activity.SetCustomProperty("firsthash", null);

                if (activity.Duration < minimumTotalDuration && !postgreSqlSettings.TelemetryAlwaysLogSqlText)
                {
                    // Remove logged parameters
                    activity.SetTag("db.command.parameters", null);
                    activity.SetTag("Parameters", null);
                }

                Activity? parent = activity.Parent;
                while (parent != null && !IsRequestDbOwner(parent))
                {
                    parent = parent.Parent;
                }

                // Transfer state to the root request activity
                if (parent != null)
                {
                    string? dbHash = activity.GetTagItem("db.hash")?.ToString();
                    if (!string.IsNullOrEmpty(dbHash))
                    {
                        Guid id = Guid.CreateVersion7();
                        parent.SetBaggage($"{DbStatsBaggagePrefix}{id}", $"{dbHash}:{activity.Duration.TotalMilliseconds:F0}");
                    }
                }
            }

            // Process root request activity: aggregate DB time and add as tag, along with individual DB command hashes and durations
            if (IsRequestDbOwner(activity))
            {
                StringBuilder sb = new();
                long totalDbDuration = 0;
                int dbActivityCount = 0;
                List<string> baggageKeys = [];
                activity.Baggage.Where(b => b.Key.StartsWith(DbStatsBaggagePrefix, StringComparison.OrdinalIgnoreCase)).OrderBy(b => b.Key).ToList().ForEach(b =>
                {
                    baggageKeys.Add(b.Key);
                    sb.Append($";{b.Value}");
                    ++dbActivityCount;
                    var parts = b.Value!.Split(':');
                    if (parts.Length == 2 && long.TryParse(parts[1], out var duration))
                    {
                        totalDbDuration += duration;
                    }
                });

                // Clear baggage to avoid propagating out of process
                foreach (var key in baggageKeys)
                {
                    activity.SetBaggage(key, null);
                }

                if (dbActivityCount > 0)
                {
                    activity.SetTag("db.stats", $"Count: {dbActivityCount}, tot dur: {totalDbDuration:N0}ms, avg dur: {totalDbDuration / dbActivityCount:N0}ms, hashes:duration: {sb.Remove(0, 1)}");
                }
            }
        }

        private static bool IsRequestDbOwner(Activity activity)
        {
            return (activity.Kind == ActivityKind.Server || activity.Kind == ActivityKind.Internal) && activity.Parent == null;
        }

        private static bool IsNpgsqlActivity(Activity a)
        {
            return a.Source.Name.Equals("Npgsql", StringComparison.OrdinalIgnoreCase) || a.OperationName.Contains("postgresql", StringComparison.OrdinalIgnoreCase);
        }
    }
}
