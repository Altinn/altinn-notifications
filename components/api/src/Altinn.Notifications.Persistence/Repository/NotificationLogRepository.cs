using System.Diagnostics.CodeAnalysis;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public class NotificationLogRepository(NpgsqlDataSource dataSource) : ITransactionalNotificationLogRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentid := @shipmentId
        )";

    /// <inheritdoc/>
    public async Task<long> InsertAsync(Guid notificationId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await ExecuteInsertAsync(notificationId, connection, transaction: null);
    }

    /// <inheritdoc/>
    public async Task<long> InsertAsync(Guid notificationId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        return await ExecuteInsertAsync(notificationId, connection, transaction);
    }

    private static async Task<long> ExecuteInsertAsync(
        Guid notificationId,
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@shipmentId", notificationId);
        var result = await command.ExecuteScalarAsync();
        return (long)result!;
    }
}
