using System.Data;
using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository for inserting and querying notification log entries.
/// </summary>
[ExcludeFromCodeCoverage]
public class NotificationLogRepository(NpgsqlDataSource dataSource) : ITransactionalNotificationLogRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentId := @shipmentId
        )";

    private const string _getNotificationLogEntriesSql = """
        SELECT
            orderchainid,
            shipmentid,
            creatorname,
            dialogid,
            transmissionid,
            operationid,
            gatewayreference,
            recipient,
            type::text,
            destination,
            resource,
            status,
            created_timestamp,
            sent_timestamp
        FROM notifications.get_notification_logs(
            _id := @id,
            _id_type := @type)
        """;

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

        command.Parameters.AddWithValue("@shipmentId", orderId);
        var result = await command.ExecuteScalarAsync();

        return result is null
            ? throw new InvalidOperationException("Database function insert_notification_log returned null.")
            : (int)Convert.ToInt64(result);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<NotificationLogEntry>> GetNotificationLogEntries(string id, NotificationLogIdType type, CancellationToken cancellationToken)
    {
        List<NotificationLogEntry> notificationLogEntries = [];
        ArgumentNullException.ThrowIfNull(id);

        await using NpgsqlCommand command = _dataSource.CreateCommand(_getNotificationLogEntriesSql);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Text, id);
        command.Parameters.AddWithValue("type", NpgsqlDbType.Text, type.ToString().ToLowerInvariant());

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                notificationLogEntries.Add(new NotificationLogEntry(
                    await reader.NullcheckAndGetValueAsync<long?>("orderchainid", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<Guid>("shipmentid", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("creatorname", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<Guid?>("dialogid", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("transmissionid", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("operationid", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("gatewayreference", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("recipient", cancellationToken),
                    await reader.GetFieldValueAsync<string>("type", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("destination", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("resource", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<string?>("status", cancellationToken),
                    await reader.GetFieldValueAsync<DateTime>("created_timestamp", cancellationToken),
                    await reader.NullcheckAndGetValueAsync<DateTime?>("sent_timestamp", cancellationToken)));
            }
        }

        return notificationLogEntries;
    }
}
