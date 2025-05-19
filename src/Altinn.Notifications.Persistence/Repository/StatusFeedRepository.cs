using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
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

        private const string _getStatusFeedSql = "select sequencenumber, orderstatus from notifications.statusfeed where sequencenumber >= $1 and creatorname=$2";

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusFeedRepository"/> class.
        /// </summary>
        /// <param name="dataSource">the npgsql data source</param>
        public StatusFeedRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        /// <summary>
        /// Get status feed
        /// </summary>
        /// <param name="seq">Start position of status feed array</param>
        /// <param name="creatorName">Name of the service owner</param>
        /// <param name="cancellationToken">Token to cancel the ongoing operation</param>
        /// <returns>List of status feed entries</returns>
        public async Task<List<StatusFeed>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken = default)
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(_getStatusFeedSql);
            command.Parameters.AddWithValue(NpgsqlDbType.Integer, seq);
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, creatorName);

            List<StatusFeed> statusFeedEntries = new();

            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var orderStatus = await reader.GetFieldValueAsync<JsonElement>("orderstatus", cancellationToken: cancellationToken);
                    var sequenceNumber = await reader.GetFieldValueAsync<int>("sequencenumber", cancellationToken: cancellationToken);
                    if (orderStatus.ValueKind != JsonValueKind.Null)
                    {
                        statusFeedEntries.Add(new StatusFeed { OrderStatus = orderStatus, SequenceNumber = sequenceNumber });
                    }
                }
            }

            return statusFeedEntries;
        }
    }
}
