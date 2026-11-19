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
    /// short operations unless SQL text or parameter logging is enabled. Parameter text is stored in non-propagating
    /// custom properties until root aggregation, when temporary state and baggage entries are cleared.</remarks>
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
        /// activity, while parameter text is stored in its non-propagating custom properties. Totals and averages are
        /// written to a database statistics tag, then temporary baggage and custom properties are cleared.</remarks>
        /// <param name="data">The completed activity to inspect, update, and optionally enrich with aggregated database telemetry.</param>
        public override void OnEnd(Activity data)
        {
            // Suppress Npgsql activities with short duration if it's not the first time we see this trace
            if (IsNpgsqlActivity(data))
            {
                var minimumTotalDuration = TimeSpan.FromMilliseconds(postgreSqlSettings.TelemetryMinDurationMs);
                bool isFirstHash = data.GetCustomProperty("firsthash") is bool firstHash && firstHash;
                if (data.Duration < minimumTotalDuration && !isFirstHash)
                {
                    // Suppress export
                    data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                }

                data.SetCustomProperty("firsthash", null);

                if (data.Duration < minimumTotalDuration && !postgreSqlSettings.TelemetryAlwaysLogSqlText && !postgreSqlSettings.TelemetryAlwaysLogSqlParameters)
                {
                    // Remove logged parameters
                    data.SetTag("db.command.parameters", null);
                    data.SetTag("Parameters", null);
                }

                Activity? parent = data.Parent;
                while (parent != null && !IsRequestDbOwner(parent))
                {
                    parent = parent.Parent;
                }

                // Transfer state to the root request activity
                if (parent != null)
                {
                    string? dbHash = data.GetTagItem("db.hash")?.ToString();
                    if (!string.IsNullOrEmpty(dbHash))
                    {
                        Guid id = Guid.CreateVersion7();
                        string key = $"{DbStatsBaggagePrefix}{id}";
                        if (postgreSqlSettings.TelemetryAlwaysLogSqlParameters && data.GetTagItem("Parameters") is string parameters)
                        {
                            parent.SetCustomProperty(key, parameters);
                        }

                        parent.SetBaggage(key, $"{dbHash}:{data.Duration.TotalMilliseconds:F0}");
                    }
                }
            }

            // Process root request activity: aggregate DB time and add as tag, along with individual DB command hashes and durations
            if (IsRequestDbOwner(data))
            {
                StringBuilder sb = new();
                long totalDbDuration = 0;
                int dbActivityCount = 0;
                List<string> baggageKeys = [];
                data.Baggage.Where(b => b.Key.StartsWith(DbStatsBaggagePrefix, StringComparison.OrdinalIgnoreCase)).OrderBy(b => b.Key).ToList().ForEach(b =>
                {
                    baggageKeys.Add(b.Key);
                    sb.Append($";{b.Value}");
                    if (postgreSqlSettings.TelemetryAlwaysLogSqlParameters && data.GetCustomProperty(b.Key) is string parameters)
                    {
                        sb.Append('#').Append(parameters);
                    }

                    ++dbActivityCount;
                    var parts = b.Value!.Split(':');
                    if (parts.Length == 2 && long.TryParse(parts[1], out var duration))
                    {
                        totalDbDuration += duration;
                    }
                });

                // Clear temporary stats and parameter state after aggregation.
                foreach (var key in baggageKeys)
                {
                    data.SetBaggage(key, null);
                    data.SetCustomProperty(key, null);
                }

                if (dbActivityCount > 0)
                {
                    string value = $"Count: {dbActivityCount}, tot dur: {totalDbDuration:N0}ms, avg dur: {totalDbDuration / dbActivityCount:N0}ms," +
                                   $" hashes:duration:{(postgreSqlSettings.TelemetryAlwaysLogSqlParameters ? "#params" : null)} {sb.Remove(0, 1)}";

                    data.SetTag("db.stats", value);
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
