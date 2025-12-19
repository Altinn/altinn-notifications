using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace Tools;

[ExcludeFromCodeCoverage]
internal static class PostgresUtil
{
    internal static async Task<bool> IsEmailNotificationInSucceededState(
  NpgsqlDataSource dataSource,
  string operationId)
    {
        const string sql = @"
            SELECT COUNT(1) 
            FROM notifications.emailnotifications 
            WHERE operationid = @operationId 
            AND result = 'Succeeded'";

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("operationId", operationId);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }
}
