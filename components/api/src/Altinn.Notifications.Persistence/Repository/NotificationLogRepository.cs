using System.Data;
using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
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
        List<NotificationLogEntry> notificationLogEntries = new();
        ArgumentNullException.ThrowIfNull(id);

        await using NpgsqlCommand command = _dataSource.CreateCommand(_getNotificationLogEntriesSql);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Text, id);
        command.Parameters.AddWithValue("type", NpgsqlDbType.Text, type.ToString().ToLowerInvariant());

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                notificationLogEntries.Add(new NotificationLogEntry(
                    await reader.GetFieldValueAsync<long?>("orderchainid", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<Guid>("shipmentid", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("creatorname", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<Guid?>("dialogid", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("transmissionid", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("operationid", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("gatewayreference", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("recipient", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string>("type", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("destination", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("resource", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<string?>("status", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<DateTime>("created_timestamp", cancellationToken: cancellationToken),
                    await reader.GetFieldValueAsync<DateTime?>("sent_timestamp", cancellationToken: cancellationToken)));
            }
        }

        return notificationLogEntries;
    }
}
