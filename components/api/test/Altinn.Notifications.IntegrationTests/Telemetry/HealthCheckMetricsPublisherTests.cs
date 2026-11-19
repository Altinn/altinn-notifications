using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Altinn.Notifications.Integrations.Telemetry;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Telemetry;

public sealed class HealthCheckMetricsPublisherTests : IDisposable
{
    private readonly HealthCheckMetricsPublisher _sut = new();

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public async Task PublishAsync_RecordsOneForHealthyAndZeroForOtherStatusesPerCheck()
    {
        // Arrange
        // Other test hosts publish to the same meter name, so use unique check names and a thread-safe collection.
        string prefix = Guid.NewGuid().ToString("N");
        var captured = new ConcurrentDictionary<string, int>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HealthCheckMetricsPublisher.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) =>
        {
            captured[(string)tags[0].Value!] = value;
        });
        listener.Start();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                [prefix + "postgres"] = new(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
                [prefix + "service_bus"] = new(HealthStatus.Unhealthy, "down", TimeSpan.Zero, null, null),
                [prefix + "degraded"] = new(HealthStatus.Degraded, null, TimeSpan.Zero, null, null)
            },
            TimeSpan.Zero);

        // Act
        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, captured[prefix + "postgres"]);
        Assert.Equal(0, captured[prefix + "service_bus"]);
        Assert.Equal(0, captured[prefix + "degraded"]);
    }
}
