namespace Altinn.Notifications.Core.Models.Metrics;

/// <summary>
/// Model for metrics for daily statistics
/// </summary>
public record DailySmsMetrics
{
    /// <summary>
    /// The day of the month the metrics apply for
    /// </summary>
    public int Day { get; init; }
    
    /// <summary>
    /// The month the metrics apply for
    /// </summary>
    public int Month { get; init; }

    /// <summary>
    /// The year the metrics apply for
    /// </summary>
    public int Year { get; init; }

    /// <summary>
    /// A list of metrics per organization
    /// </summary>
    public List<SmsRow> Metrics { get; init; } = [];
}
