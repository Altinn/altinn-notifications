using System.Diagnostics.Metrics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Integrations.Telemetry;

/// <summary>
/// Publishes the result of each registered health check as a gauge metric,
/// making dependency health visible in monitoring even when no requests reach the application.
/// </summary>
public sealed class HealthCheckMetricsPublisher : IHealthCheckPublisher, IDisposable
{
    /// <summary>
    /// The name of the meter that holds the health check metrics.
    /// </summary>
    public const string MeterName = "Altinn.Notifications.HealthCheck";

    /// <summary>
    /// The name of the gauge. The value is 1 when the check is healthy and 0 otherwise.
    /// </summary>
    public const string StatusGaugeName = "notifications_health_check_status";

    private readonly Meter _meter;

    private readonly Gauge<int> _statusGauge;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckMetricsPublisher"/> class.
    /// </summary>
    public HealthCheckMetricsPublisher()
    {
        _meter = new Meter(MeterName);
        _statusGauge = _meter.CreateGauge<int>(StatusGaugeName, description: "Health check status per check. 1 is healthy, 0 is degraded or unhealthy.");
    }

    /// <inheritdoc />
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var entry in report.Entries)
        {
            int value = entry.Value.Status == HealthStatus.Healthy ? 1 : 0;
            _statusGauge.Record(value, new KeyValuePair<string, object?>("check", entry.Key));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
