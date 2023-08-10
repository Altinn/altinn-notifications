using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace Altinn.Notifications.Persistence.Health;

/// <summary>
/// Health check service confirming Postgre connenctivity
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresHealthCheck"/> class.
    /// </summary>
    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _dataSource.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "select 1;";

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, description: ex.Message, exception: ex);
        }
    }
}