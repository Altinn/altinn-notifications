using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Health;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations;

public class PostgresHealthCheckTests
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
        using PostgresHealthCheck healthCheck = new(_dataSource);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, res.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_InvalidConnectionString_ReturnsUnhealthyResult()
    {
        var tempDataSource = new NpgsqlDataSourceBuilder("Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb").Build();

        using PostgresHealthCheck healthCheck = new(tempDataSource);
        HealthCheckResult res = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, res.Status);
    }
}
