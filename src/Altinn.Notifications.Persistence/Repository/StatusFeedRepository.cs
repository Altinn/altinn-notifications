using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository for handling status feed related operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StatusFeedRepository"/> class.
/// </remarks>
/// <param name="dataSource">the npgsql data source</param>
public class StatusFeedRepository(NpgsqlDataSource dataSource) : IStatusFeedRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    // the created column is used to only return entries that are older than 2 seconds, to avoid returning entries that are still being processed
    private const string _getStatusFeedSql = @"SELECT * FROM notifications.getstatusfeed(@seq, @creatorname, @limit)";
    private const string _deleteOldStatusFeedRecordsSql = "SELECT notifications.delete_old_status_feed_records()";
    private static readonly string _insertStatusFeedEntrySql = @"SELECT notifications.insertstatusfeed(o._id, o.creatorname, @orderstatus)
                                                                  FROM notifications.orders o
                                                                  WHERE o.alternateid = @alternateid;";

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _statusFeedSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc/>
    public async Task<List<StatusFeed>> GetStatusFeed(long seq, string creatorName, int pageSize, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(_getStatusFeedSql);
        command.Parameters.AddWithValue("seq", NpgsqlDbType.Bigint, seq);
        command.Parameters.AddWithValue("creatorName", NpgsqlDbType.Varchar, creatorName);
        command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, pageSize);

        List<StatusFeed> statusFeedEntries = [];

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var sequenceNumber = await reader.GetFieldValueAsync<long>("_id", cancellationToken: cancellationToken);
                var orderStatus = await reader.GetFieldValueAsync<string>("orderstatus", cancellationToken: cancellationToken);
                var orderStatusObj = JsonSerializer.Deserialize<OrderStatus>(orderStatus, _jsonSerializerOptions);

                if (orderStatusObj == null)
                {
                    throw new InvalidOperationException($"Deserialized OrderStatus is null for sequence number {sequenceNumber}. This indicates a data inconsistency or serialization issue.");
                }

                statusFeedEntries.Add(new StatusFeed
                {
                    SequenceNumber = sequenceNumber,
                    OrderStatus = orderStatusObj
                });
            }
        }

        return statusFeedEntries;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOldStatusFeedRecords(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(_deleteOldStatusFeedRecordsSql);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return (int)Convert.ToInt64(result);
    }

    /// <summary>
    /// Inserts a status feed entry for an order.
    /// </summary>
    /// <param name="orderStatus">The order status object to be serialized as JSONB</param>
    /// <param name="connection">The database connection to use</param>
    /// <param name="transaction">The database transaction to use</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This is a shared helper method that can be called from both NotificationRepositoryBase
    /// and OrderRepository to insert status feed entries consistently.
    /// </remarks>
    public static async Task InsertStatusFeedEntry(OrderStatus orderStatus, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(orderStatus);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using NpgsqlCommand pgcom = new(_insertStatusFeedEntrySql, connection, transaction);
        pgcom.Parameters.AddWithValue("alternateid", NpgsqlDbType.Uuid, orderStatus.ShipmentId);
        pgcom.Parameters.AddWithValue("orderstatus", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(orderStatus, _statusFeedSerializerOptions));
        var result = await pgcom.ExecuteScalarAsync();
        if (result == null)
        {
            throw new InvalidOperationException($"Failed to insert status feed entry. No order found with alternateid {orderStatus.ShipmentId}.");
        }
    }
}
