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
            _orderid := @orderId
        )";

    /// <inheritdoc/>
    public async Task<int> InsertAsync(Guid orderId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await ExecuteInsertAsync(orderId, connection, transaction: null);
    }

    /// <inheritdoc/>
    public async Task<int> InsertAsync(Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        return await ExecuteInsertAsync(orderId, connection, transaction);
    }

    /// <summary>
    /// Inserts a notification log entry for the specified order.
    /// </summary>
    /// <param name="orderId">The ID of the order for which to insert the notification log entry.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The database transaction to use.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public static async Task InsertNotificationLogEntry(Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await ExecuteInsertAsync(orderId, connection, transaction);
    }

    private static async Task<int> ExecuteInsertAsync(
        Guid orderId,
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@orderId", orderId);
        var result = await command.ExecuteScalarAsync();

        return result is null
            ? throw new InvalidOperationException("Database function insert_notification_log returned null.")
            : (int)Convert.ToInt64(result);
    }
}
