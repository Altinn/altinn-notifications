namespace Altinn.Notifications.Core.Models.Metrics;

/// <summary>
/// Model for metrics for org
/// </summary>
public class MonthlyNotificationMetrics
{
    /// <summary>
    /// The month the metrics apply for
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// The year the metrics apply for
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// A list of metrics per organization
    /// </summary>
    public List<MetricsForOrg> Metrics { get; set; } = [];
}
