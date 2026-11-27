using System.Diagnostics.Metrics;

using Altinn.Notifications.Integrations.Telemetry;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Telemetry;

public sealed class HealthCheckMetricsPublisherTests : IDisposable
{
    private readonly HealthCheckMetricsPublisher _healthCheckMetricsPublisher = new();

    public void Dispose()
    {
        _healthCheckMetricsPublisher.Dispose();
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, 1)]
    [InlineData(HealthStatus.Degraded, 0)]
    [InlineData(HealthStatus.Unhealthy, 0)]
    public async Task PublishAsync_RecordsOneForHealthyAndZeroForOtherStatuses(HealthStatus status, int expected)
    {
        var measurements = new List<(string Check, int Value)>();
        using var listener = CreateListener(measurements);

        await _healthCheckMetricsPublisher.PublishAsync(CreateReport(("check_a", status)), TestContext.Current.CancellationToken);

        Assert.Equal([("check_a", expected)], measurements);
    }

    [Fact]
    public async Task PublishAsync_RecordsOneMeasurementPerCheckTaggedWithItsName()
    {
        var measurements = new List<(string Check, int Value)>();
        using var listener = CreateListener(measurements);

        await _healthCheckMetricsPublisher.PublishAsync(
            CreateReport(("postgres", HealthStatus.Healthy), ("service_bus", HealthStatus.Unhealthy)),
            TestContext.Current.CancellationToken);

        Assert.Equal([("postgres", 1), ("service_bus", 0)], measurements.OrderBy(m => m.Check));
    }

    /// <summary>
    /// Creates and starts a listener that records health-check measurements from the publisher's meter.
    /// </summary>
    /// <param name="measurements">The collection to receive each check name and recorded value.</param>
    /// <returns>The started meter listener.</returns>
    private static MeterListener CreateListener(List<(string Check, int Value)> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == HealthCheckMetricsPublisher.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) =>
        {
            var check = tags.ToArray().Single(t => t.Key == HealthCheckMetricsPublisher.CheckTagName);
            measurements.Add(((string)check.Value!, value));
        });

        listener.Start();
        return listener;
    }

    /// <summary>
    /// Creates a health report containing the specified named check results.
    /// </summary>
    /// <param name="checks">The check names and statuses to include in the report.</param>
    /// <returns>The health report built from the supplied checks.</returns>
    private static HealthReport CreateReport(params (string Name, HealthStatus Status)[] checks)
    {
        var entries = checks.ToDictionary(
            c => c.Name,
            c => new HealthReportEntry(c.Status, null, TimeSpan.Zero, null, null));

        return new HealthReport(entries, TimeSpan.Zero);
    }
}
