using System.Diagnostics;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Telemetry;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingTelemetry;

public class NpgsqlProcessorTests
{
    [Fact]
    public void OnEnd_ShortNpgsqlActivityWithoutFirstHash_SuppressesSpanAndRemovesParameters()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
        using var activity = new Activity("postgresql query").Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.SetTag("db.command.parameters", "p=1");

        processor.OnEnd(activity);

        Assert.Equal(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
        Assert.Null(activity.GetTagItem("db.command.parameters"));
    }

    [Fact]
    public void OnEnd_ShortNpgsqlActivityWithLocalFirstHashMarker_KeepsRecordedFlagAndClearsMarker()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
        using var activity = new Activity("postgresql query").Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.SetCustomProperty("firsthash", true);

        processor.OnEnd(activity);

        Assert.NotEqual(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
        Assert.Null(activity.GetCustomProperty("firsthash"));
    }

    [Fact]
    public void OnEnd_NpgsqlActivityWithInheritedFirstHashBaggage_SuppressesSpan()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
        using var root = new Activity("request").Start();
        root.SetBaggage("firsthash", "true");
        using var child = new Activity("postgresql query").Start();
        child.ActivityTraceFlags = ActivityTraceFlags.Recorded;

        processor.OnEnd(child);

        Assert.Equal(0, (int)(child.ActivityTraceFlags & ActivityTraceFlags.Recorded));
        Assert.Equal("true", root.GetBaggageItem("firsthash"));
    }

    [Fact]
    public void OnEnd_NpgsqlActivityWithRootParent_TransfersHashAndDurationToRootBaggage()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
        using var root = new Activity("request").Start();
        using var child = new Activity("postgresql query").Start();
        child.SetTag("db.hash", "abc");

        processor.OnEnd(child);

        var hashEntries = root.Baggage.Where(entry => entry.Key.StartsWith("npgsql.processor.hash.", StringComparison.OrdinalIgnoreCase)).ToList();
        var hashEntry = Assert.Single(hashEntries);
        Assert.StartsWith("abc:", hashEntry.Value);
    }

    [Fact]
    public void OnEnd_NpgsqlActivityWithoutHash_DoesNotTransferCommandStatsToRootBaggage()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
        using var root = new Activity("request").Start();
        using var child = new Activity("postgresql query").Start();

        processor.OnEnd(child);

        var hashEntries = root.Baggage.Where(entry => entry.Key.StartsWith("npgsql.processor.hash.", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(hashEntries);
    }

    [Fact]
    public void OnEnd_RootActivityWithReservedDbHashBaggage_AddsDbStatsAndClearsOnlyReservedBaggage()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: 1);
        using var root = new Activity("request").Start();
        root.SetBaggage("npgsql.processor.hash.A", "h1:10");
        root.SetBaggage("npgsql.processor.hash.B", "h2:20");
        root.SetBaggage("hashUser", "user-hash-value");

        processor.OnEnd(root);

        string? dbStats = root.GetTagItem("db.stats")?.ToString();
        Assert.NotNull(dbStats);
        Assert.Contains("Count: 2", dbStats);
        Assert.Contains("tot dur: 30ms", dbStats);
        Assert.Contains("avg dur: 15ms", dbStats);
        Assert.Null(root.GetBaggageItem("npgsql.processor.hash.A"));
        Assert.Null(root.GetBaggageItem("npgsql.processor.hash.B"));
        Assert.Equal("user-hash-value", root.GetBaggageItem("hashUser"));
    }

    private static NpgsqlProcessor CreateProcessor(int telemetryMinDurationMs)
    {
        return new NpgsqlProcessor(new PostgreSqlSettings
        {
            TelemetryMinDurationMs = telemetryMinDurationMs,
        });
    }
}
