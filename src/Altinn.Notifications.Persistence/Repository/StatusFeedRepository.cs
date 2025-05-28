using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Repository for handling status feed related operations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class StatusFeedRepository : IStatusFeedRepository
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<StatusFeedRepository> _logger;

        // the created column is used to only return entries that are older than 2 seconds, to avoid returning entries that are still being processed
        private const string _getStatusFeedSql = @"SELECT _id, orderstatus
                                                   FROM notifications.statusfeed
                                                   WHERE _id > @seq
                                                     AND creatorname = @creatorName
                                                     AND created < (NOW() - INTERVAL '2 seconds')
                                                   ORDER BY _id
                                                   LIMIT @limit;";

        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusFeedRepository"/> class.
        /// </summary>
        /// <param name="dataSource">the npgsql data source</param>
        /// <param name="logger">The logger associated with this implementation</param>
        public StatusFeedRepository(NpgsqlDataSource dataSource, ILogger<StatusFeedRepository> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<StatusFeed>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken, int limit = 50)
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(_getStatusFeedSql);
            command.Parameters.AddWithValue("@seq", NpgsqlDbType.Integer, seq);
            command.Parameters.AddWithValue("@creatorName", NpgsqlDbType.Varchar, creatorName);
            command.Parameters.AddWithValue("@limit", NpgsqlDbType.Integer, limit);

            List<StatusFeed> statusFeedEntries = new();

            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    try
                    {
                        var sequenceNumber = await reader.GetFieldValueAsync<int>("_id", cancellationToken: cancellationToken);
                        var orderStatus = await reader.GetFieldValueAsync<string>("orderstatus", cancellationToken: cancellationToken);
                        var orderStatusObj = JsonSerializer.Deserialize<OrderStatus>(orderStatus, _jsonSerializerOptions);

                        if (orderStatusObj == null)
                        {
                            _logger.LogError("Deserialized OrderStatus is null for sequence number {SequenceNumber}. Skipping entry.", sequenceNumber);
                            continue;
                        }

                        statusFeedEntries.Add(new StatusFeed
                        {
                            SequenceNumber = sequenceNumber,
                            OrderStatus = orderStatusObj
                        });
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error reading status feed entry from database. Skipping entry.");
                        continue;
                    }
                }
            }

            return statusFeedEntries;
        }
    }
}
