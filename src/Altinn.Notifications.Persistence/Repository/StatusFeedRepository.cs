using System.Data;
using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Models.Status;
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

        private const string _getStatusFeedSql = "select orderstatus from notifications.statusfeed where _id > $1 and creatorname=$2";

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
        public async Task<List<OrderStatus>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken = default)
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(_getStatusFeedSql);
            command.Parameters.AddWithValue(NpgsqlDbType.Integer, seq);
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, creatorName);

            List<OrderStatus> statusFeedEntries = new();

            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                // Process the data from the reader
                Console.WriteLine("hello");
                while (await reader.ReadAsync(cancellationToken))
                {
                    var orderStatus = reader.GetFieldValue<OrderStatus>("orderstatus");
                    if (orderStatus != null)
                    {
                        statusFeedEntries.Add(orderStatus);
                    }
                }
            }

            return statusFeedEntries;
        }
    }
}
