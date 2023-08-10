using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace Altinn.Notifications.Persistence.Health;

/// <summary>
/// Health check service confirming Postgre connenctivity
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly int _marker = new Random().Next(150);
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresHealthCheck"/> class.
    /// </summary>
    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
        Console.WriteLine("// PostgresHealthCheck // Constructor completed");
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine($"// PostgresHealthCheck // Check Health {_marker}-{_count}");
            _count++;

            await using var connection = _dataSource.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "select 1;";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            Console.WriteLine($"// PostgresHealthCheck //Is healthy {result!.ToString()}");

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            Console.WriteLine("// PostgresHealthCheck // Check Health failed");

            return new HealthCheckResult(context.Registration.FailureStatus, description: ex.Message, exception: ex);
        }
    }
}