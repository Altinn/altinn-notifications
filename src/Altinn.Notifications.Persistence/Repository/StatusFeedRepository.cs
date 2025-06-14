﻿using System.Data;
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
public class StatusFeedRepository : IStatusFeedRepository
{
    private readonly NpgsqlDataSource _dataSource;

    // the created column is used to only return entries that are older than 2 seconds, to avoid returning entries that are still being processed
    private const string _getStatusFeedSql = @"SELECT * FROM notifications.getstatusfeed(@seq, @creatorname, @limit)";

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFeedRepository"/> class.
    /// </summary>
    /// <param name="dataSource">the npgsql data source</param>
    public StatusFeedRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<List<StatusFeed>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken, int limit = 50)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(_getStatusFeedSql);
        command.Parameters.AddWithValue("seq", NpgsqlDbType.Integer, seq);
        command.Parameters.AddWithValue("creatorName", NpgsqlDbType.Varchar, creatorName);
        command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, limit);

        List<StatusFeed> statusFeedEntries = [];

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var sequenceNumber = await reader.GetFieldValueAsync<int>("_id", cancellationToken: cancellationToken);
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
}
