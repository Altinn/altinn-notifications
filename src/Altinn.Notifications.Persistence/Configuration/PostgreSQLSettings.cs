namespace Altinn.Notifications.Persistence.Configuration;

/// <summary>
/// Settings for Postgre database
/// </summary>
public class PostgreSqlSettings
{
    /// <summary>
    /// Connection string for the postgre db
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Password for app user for the postgre db
    /// </summary>
    public string NotificationsDbPwd { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to include parameter values in logging/tracing.
    /// </summary>
    public bool LogParameters { get; set; } = false;
}