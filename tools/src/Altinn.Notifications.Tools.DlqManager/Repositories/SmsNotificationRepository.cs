using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Tools.DlqManager.Repositories;

/// <summary>
/// Npgsql-based implementation of <see cref="ISmsNotificationRepository"/>.
/// Only contains the two targeted queries needed by the DLQ Manager — deliberately
/// minimal to avoid coupling to the full Persistence component.
/// </summary>
public class SmsNotificationRepository(NpgsqlDataSource dataSource) : ISmsNotificationRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    /// <inheritdoc/>
    public async Task<(string? Result, DateTime? ExpiryTime, DateTime? ResultTime)> GetNotificationStateAsync(
        Guid notificationId)
    {
        const string sql = """
            SELECT result, expirytime, resulttime
            FROM notifications.smsnotifications
            WHERE alternateid = @notificationId
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter("notificationId", NpgsqlDbType.Uuid) { Value = notificationId });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var result     = await reader.IsDBNullAsync(0) ? null : await reader.GetFieldValueAsync<string>(0);
            var expiryTime = await reader.IsDBNullAsync(1) ? (DateTime?)null : DateTime.SpecifyKind(await reader.GetFieldValueAsync<DateTime>(1), DateTimeKind.Utc);
            var resultTime = await reader.IsDBNullAsync(2) ? (DateTime?)null : DateTime.SpecifyKind(await reader.GetFieldValueAsync<DateTime>(2), DateTimeKind.Utc);
            return (result, expiryTime, resultTime);
        }

        return (null, null, null);
    }

    /// <inheritdoc/>
    public async Task<int> UpdateResultToAcceptedAsync(Guid notificationId)
    {
        // Only updates notifications whose result is still 'Sending' and whose expiry time
        // is in the past. The API sets the result to 'Sending' before placing the command on
        // the queue, and it stays 'Sending' on DLQ because ProcessSendResult only runs when
        // the gateway call returns a response — not when it throws. The expiry guard prevents
        // accidentally advancing a notification that is still within its delivery window.
        // 'Accepted' notifications (awaiting a delivery report) and all terminal states are
        // excluded implicitly by the 'Sending' filter.
        const string sql = """
            UPDATE notifications.smsnotifications
            SET    result     = 'Accepted',
                   resulttime = NOW()
            WHERE  alternateid = @notificationId
              AND  result      = 'Sending'
              AND  expirytime  < NOW()
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter("notificationId", NpgsqlDbType.Uuid) { Value = notificationId });

        return await cmd.ExecuteNonQueryAsync();
    }
}
