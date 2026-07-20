using System.Diagnostics.CodeAnalysis;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository for inserting notification log entries.
/// </summary>
[ExcludeFromCodeCoverage]
public static class NotificationLogRepository
{
    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentId := @shipmentId
        )";

    /// <summary>
    /// Inserts notification log entries derived from the email/sms notifications for the specified shipment.
    /// </summary>
    /// <param name="shipmentId">The alternate ID of the order to insert notification log entries for.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The database transaction to use.</param>
    /// <returns>
    /// The notification ids that were skipped because a log entry already existed for them (idempotent no-op).
    /// Empty when every notification for the shipment was logged successfully.
    /// </returns>
    /// <remarks>
    /// This is a shared helper method that can be called from both NotificationRepositoryBase
    /// and OrderRepository to insert notification log entries consistently.
    /// </remarks>
    public static async Task<IReadOnlyList<Guid>> InsertNotificationLogEntry(Guid shipmentId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@shipmentId", shipmentId);
        var result = await command.ExecuteScalarAsync();

        return result is null or DBNull ? [] : (Guid[])result;
    }
}
