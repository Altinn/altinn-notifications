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

        private const string _getStatusFeedSql = "select sequencenumber, orderstatus from notifications.statusfeed where sequencenumber >= @seq and creatorname=@creatorName order by sequencenumber limit @limit";

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
            command.Parameters.AddWithValue("@seq", NpgsqlDbType.Integer, seq);
            command.Parameters.AddWithValue("@creatorName", NpgsqlDbType.Varchar, creatorName);
            command.Parameters.AddWithValue("@limit", NpgsqlDbType.Integer, limit);

            List<StatusFeed> statusFeedEntries = [];

            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var orderStatus = await reader.GetFieldValueAsync<string>("orderstatus", cancellationToken: cancellationToken);
                    var sequenceNumber = await reader.GetFieldValueAsync<int>("sequencenumber", cancellationToken: cancellationToken);
                    if (!string.IsNullOrWhiteSpace(orderStatus))
                    {
                        statusFeedEntries.Add(new StatusFeed { OrderStatus = orderStatus, SequenceNumber = sequenceNumber });
                    }
                }
            }

            return statusFeedEntries;
        }
    }
}
