using System.Diagnostics.CodeAnalysis;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository for inserting and querying notification log entries.
/// </summary>
[ExcludeFromCodeCoverage]
public static class NotificationLogRepository
{
    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentId := @shipmentId
        )";

    /// <summary>
    /// Inserts a notification log entry for the specified order.
    /// </summary>
    /// <param name="orderId">The ID of the order for which to insert the notification log entry.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The database transaction to use.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public static async Task<int> InsertNotificationLogEntry(Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@shipmentId", orderId);
        var result = await command.ExecuteScalarAsync();

        return result is null
            ? throw new InvalidOperationException("Database function insert_notification_log returned null.")
            : (int)Convert.ToInt64(result);
    }
}
