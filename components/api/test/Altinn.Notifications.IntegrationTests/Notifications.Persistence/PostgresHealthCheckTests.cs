using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Health;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class PostgresHealthCheckTests : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHealthCheckTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
                       .AddJsonFile("appsettings.json")
                       .Build();

        PostgreSqlSettings? settings = configuration.GetSection("PostgreSqlSettings").Get<PostgreSqlSettings>();

        string connectionString = string.Format(settings!.ConnectionString, settings.NotificationsDbPwd);

        _dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyResult()
    {
        PostgresHealthCheck healthCheck = new(_dataSource);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, res.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_UnreachableDatabase_ReturnsUnhealthyResult()
    {
        await using var unreachable = new NpgsqlDataSourceBuilder("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d;Timeout=1").Build();

        PostgresHealthCheck healthCheck = new(unreachable);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(res.Exception);
        Assert.Equal(HealthStatus.Unhealthy, res.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsUnhealthyResult()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        PostgresHealthCheck healthCheck = new(_dataSource);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        Assert.Equal(HealthStatus.Unhealthy, res.Status);
        Assert.IsType<OperationCanceledException>(res.Exception, exactMatch: false);
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }
}
