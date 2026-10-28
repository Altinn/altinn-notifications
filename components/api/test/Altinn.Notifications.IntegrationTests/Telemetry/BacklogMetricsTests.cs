using System.Diagnostics.Metrics;

using Altinn.Notifications.Integrations.Telemetry;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Telemetry;

public sealed class BacklogMetricsTests : IDisposable
{
    private readonly BacklogMetrics _sut;

    public BacklogMetricsTests()
    {
        _sut = new BacklogMetrics();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public void RecordBacklogMetrics_EmitsAllExpectedGaugesWithTotals()
    {
        // Arrange
        var capturedValues = new Dictionary<string, long>();

        using var listener = CreateListener((instrument, value, _) =>
        {
            capturedValues[instrument.Name] = value;
        });

        // Act
        _sut.RecordBacklogMetrics(
            emailNotificationsAnytime: 3,
            emailNotificationsDaytime: 4,
            emailNotificationsComposed: 5,
            smsNotificationsAnytime: 6,
            smsNotificationsDaytime: 7,
            ordersNotDue: 8,
            ordersPastDue: 9,
            ordersFailed: 10);

        // Assert
        Assert.Equal(9, capturedValues.Count);
        Assert.Equal(3L, capturedValues[BacklogMetrics.EmailNotificationsAnytimeGaugeName]);
        Assert.Equal(4L, capturedValues[BacklogMetrics.EmailNotificationsDaytimeGaugeName]);
        Assert.Equal(12L, capturedValues[BacklogMetrics.EmailNotificationsTotalGaugeName]);
        Assert.Equal(6L, capturedValues[BacklogMetrics.SmsNotificationsAnytimeGaugeName]);
        Assert.Equal(7L, capturedValues[BacklogMetrics.SmsNotificationsDaytimeGaugeName]);
        Assert.Equal(13L, capturedValues[BacklogMetrics.SmsNotificationsTotalGaugeName]);
        Assert.Equal(8L, capturedValues[BacklogMetrics.OrdersNotDueGaugeName]);
        Assert.Equal(9L, capturedValues[BacklogMetrics.OrdersPastDueGaugeName]);
        Assert.Equal(10L, capturedValues[BacklogMetrics.OrdersFailedGaugeName]);
    }

    private static MeterListener CreateListener(
        Action<Instrument, long, ReadOnlySpan<KeyValuePair<string, object?>>> onMeasurement)
    {
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BacklogMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            onMeasurement(instrument, value, tags);
        });

        listener.Start();
        return listener;
    }
}
