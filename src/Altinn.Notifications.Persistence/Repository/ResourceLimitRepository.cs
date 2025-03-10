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
            await pgcom.ExecuteNonQueryAsync();

            return true;
        }
    }
}
