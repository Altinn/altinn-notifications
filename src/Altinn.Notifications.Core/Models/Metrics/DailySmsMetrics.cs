namespace Altinn.Notifications.Core.Models.Metrics;

/// <summary>
/// Model for metrics for daily statistics
/// </summary>
public class DailySmsMetrics
{
    /// <summary>
    /// The day of the month the metrics apply for
    /// </summary>
    public int Day { get; set; }
    
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
    public List<SmsRow> Metrics { get; set; } = [];
}
