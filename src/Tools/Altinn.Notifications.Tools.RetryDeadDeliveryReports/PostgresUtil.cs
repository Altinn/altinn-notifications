using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Tools.RetryDeadDeliveryReports;

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
            AND expirytime < (now() - make_interval(secs => @expiryOffsetSeconds))
            AND result = 'Succeeded'";

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("operationId", operationId);
        command.Parameters.AddWithValue("expiryOffsetSeconds", 300); // default offset

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }
}
