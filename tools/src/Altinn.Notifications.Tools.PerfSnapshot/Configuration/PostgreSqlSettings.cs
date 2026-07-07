namespace Altinn.Notifications.Tools.PerfSnapshot.Configuration;

/// <summary>
/// PostgreSQL connection settings for the perf snapshot tool.
/// </summary>
public class PostgreSqlSettings
{
    /// <summary>
    /// Full PostgreSQL connection string, including host, port, username, password and database.
    /// Override via user secrets when targeting non-localhost environments.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
