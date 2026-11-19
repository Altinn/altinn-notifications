using System.Diagnostics;
using System.Text.Json;
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
    public void Enrich_WithoutParameters_DoesNotAddParametersTag()
    {
        var enricher = CreateEnricher(telemetryAlwaysLogSqlText: false);
        using var activity = new Activity("db").Start();
        using var command = new NpgsqlCommand("SELECT 1");

        enricher.Enrich(activity, command);

        Assert.Null(activity.GetTagItem("Parameters"));
        Assert.Equal("SELECT 1", activity.GetTagItem("db.statement0"));
        Assert.IsType<string>(activity.GetTagItem("db.hash"));
    }

    [Fact]
    public void Enrich_WithParameters_SerializesAllParameterIdsNamesAndValues()
    {
        var enricher = CreateEnricher(telemetryAlwaysLogSqlText: false);
        using var activity = new Activity("db").Start();
        using var command = CreateCommand("SELECT @p, @message", 42);
        command.Parameters.AddWithValue("message", "value:with#delimiters;and\"quotes");

        enricher.Enrich(activity, command);

        string parameters = Assert.IsType<string>(activity.GetTagItem("Parameters"));
        using var document = JsonDocument.Parse(parameters);
        var items = document.RootElement;
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(0, items[0].GetProperty("Id").GetInt32());
        Assert.Equal("p", items[0].GetProperty("ParameterName").GetString());
        Assert.Equal(42, items[0].GetProperty("Value").GetInt32());
        Assert.Equal(1, items[1].GetProperty("Id").GetInt32());
        Assert.Equal("message", items[1].GetProperty("ParameterName").GetString());
        Assert.Equal("value:with#delimiters;and\"quotes", items[1].GetProperty("Value").GetString());
    }

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
