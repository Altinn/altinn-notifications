using System.Diagnostics.CodeAnalysis;
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

        private const string GetStatusFeedSql = @"
            SELECT * FROM notifications.statusfeed
            WHERE seq >= $1 AND seq <= $2
            ORDER BY seq ASC
            LIMIT 50;";

        /// <summary>
        /// Get status feed
        /// </summary>
        /// <param name="seq">Start position of status feed array</param>
        /// <param name="creatorName">Name of the service owner</param>
        /// <param name="cancellationToken">Token to cancel the ongoing operation</param>
        /// <returns>List of status feed entries</returns>
        public async Task<List<object>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken = default)
        {
            await using NpgsqlCommand command = _dataSource.CreateCommand(GetStatusFeedSql);
            command.Parameters.AddWithValue(NpgsqlDbType.Integer, seq);
            command.Parameters.AddWithValue(NpgsqlDbType.Varchar, creatorName);

            List<object> statusFeedEntries = new List<object>();

            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    // Process the data from the reader
                }
            }

            return statusFeedEntries;
        }
    }
}
