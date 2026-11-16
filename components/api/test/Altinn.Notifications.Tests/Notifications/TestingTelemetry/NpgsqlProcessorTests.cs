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

    [Theory]
    [InlineData(false, 10)]
    [InlineData(false, 200)]
    [InlineData(true, 10)]
    [InlineData(true, 200)]
    public void OnEnd_NpgsqlActivityWithParameters_TransfersParametersOnlyWhenEnabled(bool logParameters, int durationMs)
    {
        const string parameters = "[{\"Id\":0,\"ParameterName\":\"p\",\"Value\":42}]";
        var processor = CreateProcessor(telemetryMinDurationMs: 100, telemetryAlwaysLogSqlParameters: logParameters);
        using var root = new Activity("request").Start();
        using var child = new Activity("postgresql query").Start();
        child.SetTag("db.hash", "abc");
        child.SetTag("Parameters", parameters);
        child.SetEndTime(child.StartTimeUtc.AddMilliseconds(durationMs));
        child.Stop();

        processor.OnEnd(child);

        var hashEntry = Assert.Single(root.Baggage, entry => entry.Key.StartsWith("npgsql.processor.hash.", StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"abc:{durationMs}{(logParameters ? $"#{parameters}" : string.Empty)}", hashEntry.Value);
        if (logParameters || durationMs >= 100)
        {
            Assert.Equal(parameters, child.GetTagItem("Parameters"));
        }
        else
        {
            Assert.Null(child.GetTagItem("Parameters"));
        }
    }

    [Fact]
    public void OnEnd_NpgsqlActivityWithoutParameters_WhenParameterLoggingEnabled_TransfersHashAndDurationOnly()
    {
        var processor = CreateProcessor(telemetryMinDurationMs: 100, telemetryAlwaysLogSqlParameters: true);
        using var root = new Activity("request").Start();
        using var child = new Activity("postgresql query").Start();
        child.SetTag("db.hash", "abc");
        child.SetEndTime(child.StartTimeUtc.AddMilliseconds(10));
        child.Stop();

        processor.OnEnd(child);

        var hashEntry = Assert.Single(root.Baggage, entry => entry.Key.StartsWith("npgsql.processor.hash.", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("abc:10", hashEntry.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnEnd_RootActivityWithParameterStats_PreservesDurationsAndClearsBaggage(bool logParameters)
    {
        const string parameters = "[{\"Id\":0,\"ParameterName\":\"p\",\"Value\":\"value:with#delimiters;\"}]";
        var processor = CreateProcessor(telemetryMinDurationMs: 0, telemetryAlwaysLogSqlParameters: logParameters);
        using var root = new Activity("request").Start();
        using var firstChild = new Activity("postgresql query").Start();
        firstChild.SetTag("db.hash", "h1");
        firstChild.SetTag("Parameters", parameters);
        firstChild.SetEndTime(firstChild.StartTimeUtc.AddMilliseconds(10));
        firstChild.Stop();
        processor.OnEnd(firstChild);
        using var secondChild = new Activity("postgresql query").Start();
        secondChild.SetTag("db.hash", "h2");
        secondChild.SetEndTime(secondChild.StartTimeUtc.AddMilliseconds(20));
        secondChild.Stop();
        processor.OnEnd(secondChild);
        root.SetBaggage("unrelated", "keep");

        processor.OnEnd(root);

        string stats = Assert.IsType<string>(root.GetTagItem("db.stats"));
        string expectedFirstEntry = $"h1:10{(logParameters ? $"#{parameters}" : string.Empty)}";
        string expectedHeader = $"Count: 2, tot dur: 30ms, avg dur: 15ms, hashes:duration:{(logParameters ? "#params" : string.Empty)} ";
        Assert.Contains(stats, new[] { $"{expectedHeader}{expectedFirstEntry};h2:20", $"{expectedHeader}h2:20;{expectedFirstEntry}" });
        var baggageEntry = Assert.Single(root.Baggage);
        Assert.Equal("unrelated", baggageEntry.Key);
        Assert.Equal("keep", baggageEntry.Value);
    }

    [Fact]
    public void PostgreSqlSettings_ByDefault_DoesNotEnableSqlParameterLogging()
    {
        Assert.False(new PostgreSqlSettings().TelemetryAlwaysLogSqlParameters);
    }

    private static NpgsqlProcessor CreateProcessor(int telemetryMinDurationMs, bool telemetryAlwaysLogSqlParameters = false)
    {
        return new NpgsqlProcessor(new PostgreSqlSettings
        {
            TelemetryMinDurationMs = telemetryMinDurationMs,
            TelemetryAlwaysLogSqlParameters = telemetryAlwaysLogSqlParameters,
        });
    }
}
