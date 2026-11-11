using System.Diagnostics;
using Altinn.Notifications.Telemetry;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingTelemetry;

public class NpgsqlProcessorTests
{
    [Fact]
    public void OnEnd_ShortNpgsqlActivityWithoutFirstHash_SuppressesSpanAndRemovesParameters()
    {
        var processor = new NpgsqlProcessor(TimeSpan.FromHours(1));
        using var activity = new Activity("postgresql query").Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.SetTag("db.command.parameters", "p=1");

        processor.OnEnd(activity);

        Assert.Equal(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
        Assert.Null(activity.GetTagItem("db.command.parameters"));
    }

    [Fact]
    public void OnEnd_ShortNpgsqlActivityWithFirstHash_KeepsRecordedFlagAndClearsBaggage()
    {
        var processor = new NpgsqlProcessor(TimeSpan.FromHours(1));
        using var activity = new Activity("postgresql query").Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.AddBaggage("firsthash", "true");

        processor.OnEnd(activity);

        Assert.NotEqual(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
        Assert.Null(activity.GetBaggageItem("firsthash"));
    }

    [Fact]
    public void OnEnd_NpgsqlActivityWithRootParent_TransfersHashAndDurationToRootBaggage()
    {
        var processor = new NpgsqlProcessor(TimeSpan.FromHours(1));
        using var root = new Activity("request").Start();
        using var child = new Activity("postgresql query").Start();
        child.SetTag("db.hash", "abc");

        processor.OnEnd(child);

        var hashEntries = root.Baggage.Where(entry => entry.Key.StartsWith("hash", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(hashEntries);
        Assert.StartsWith("abc:", hashEntries[0].Value);
    }

    [Fact]
    public void OnEnd_RootActivityWithHashBaggage_AddsDbStatsAndClearsHashBaggage()
    {
        var processor = new NpgsqlProcessor(TimeSpan.FromMilliseconds(1));
        using var root = new Activity("request").Start();
        root.SetBaggage("hashA", "h1:10");
        root.SetBaggage("hashB", "h2:20");

        processor.OnEnd(root);

        string? dbStats = root.GetTagItem("db.stats")?.ToString();
        Assert.NotNull(dbStats);
        Assert.Contains("Count: 2", dbStats);
        Assert.Contains("tot dur: 30ms", dbStats);
        Assert.Contains("avg dur: 15ms", dbStats);
        Assert.Null(root.GetBaggageItem("hashA"));
        Assert.Null(root.GetBaggageItem("hashB"));
    }
}
