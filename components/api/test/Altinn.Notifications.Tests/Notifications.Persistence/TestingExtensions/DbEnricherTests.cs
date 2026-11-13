using System.Diagnostics;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class DbEnricherTests
{
    [Fact]
    public void Enrich_FirstObservedHash_AddsFirstHashLocalMarkerAndStatementTag()
    {
        var enricher = CreateEnricher(telemetryAlwaysLogSqlText: false);
        using var activity = new Activity("db").Start();
        using var command = CreateCommand("SELECT @p", 1);

        enricher.Enrich(activity, command);

        Assert.True(activity.GetCustomProperty("firsthash") is true);
        Assert.NotNull(activity.GetTagItem("db.statement0"));
        Assert.NotNull(activity.GetTagItem("db.hash"));
        Assert.Contains("ParameterName", activity.GetTagItem("Parameters")?.ToString());
    }

    [Fact]
    public void Enrich_SecondObservedHashWithoutAlwaysLog_DoesNotAddFirstHashOrStatementTag()
    {
        var enricher = CreateEnricher(telemetryAlwaysLogSqlText: false);
        using var command = CreateCommand("SELECT @p", 1);

        using var firstActivity = new Activity("db").Start();
        enricher.Enrich(firstActivity, command);
        firstActivity.Stop();

        using var secondActivity = new Activity("db").Start();
        enricher.Enrich(secondActivity, command);

        Assert.Null(secondActivity.GetCustomProperty("firsthash"));
        Assert.Null(secondActivity.GetTagItem("db.statement0"));
        Assert.NotNull(secondActivity.GetTagItem("db.hash"));
    }

    [Fact]
    public void Enrich_SecondObservedHashWithAlwaysLog_AddsStatementTag()
    {
        var enricher = CreateEnricher(telemetryAlwaysLogSqlText: true);
        using var command = CreateCommand("SELECT @p", 1);

        using var firstActivity = new Activity("db").Start();
        enricher.Enrich(firstActivity, command);
        firstActivity.Stop();

        using var secondActivity = new Activity("db").Start();
        enricher.Enrich(secondActivity, command);

        Assert.NotNull(secondActivity.GetTagItem("db.statement0"));
        Assert.NotNull(secondActivity.GetTagItem("db.hash"));
    }

    private static DbEnricher CreateEnricher(bool telemetryAlwaysLogSqlText)
    {
        var logger = new Mock<ILogger<DbEnricher>>();
        var options = Options.Create(new PostgreSqlSettings
        {
            TelemetryAlwaysLogSqlText = telemetryAlwaysLogSqlText,
        });
        return new DbEnricher(logger.Object, options);
    }

    private static NpgsqlCommand CreateCommand(string commandText, int value)
    {
        var command = new NpgsqlCommand(commandText);
        command.Parameters.AddWithValue("p", value);
        return command;
    }
}
