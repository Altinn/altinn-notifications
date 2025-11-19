using Altinn.Notifications.Core.Persistence;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Implementation of the <see cref="IResourceLimitRepository"/> interface
    /// </summary>
    public class ResourceLimitRepository : IResourceLimitRepository
    {
        private readonly NpgsqlDataSource _dataSource;
        private const string _setEmailTimeout = @"UPDATE notifications.resourcelimitlog
                                                SET emaillimittimeout = $1
                                                WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);";

        private const string _insertEmailTimeout = @"INSERT INTO notifications.resourcelimitlog (emaillimittimeout)
                                                  VALUES ($1);";

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceLimitRepository"/> class.
        /// </summary>
        public ResourceLimitRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        /// <inheritdoc/>
        public async Task<bool> SetEmailTimeout(DateTime timeout)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setEmailTimeout);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, timeout);
            var rowsAffected = await pgcom.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                return await InsertEmailTimeout(timeout);
            }

            return rowsAffected > 0;
        }

        private async Task<bool> InsertEmailTimeout(DateTime timeout)
        {
            await using NpgsqlCommand insertCom = _dataSource.CreateCommand(_insertEmailTimeout);
            insertCom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, timeout);
            var affectedRows = await insertCom.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }
    }
}
