using Altinn.Notifications.Core.Repository.Interfaces;

using Microsoft.ApplicationInsights;

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
        private readonly TelemetryClient? _telemetryClient;
        private const string _setEmailTimeout = @"UPDATE notifications.resourcelimitlog
                                                SET emaillimittimeout = $1
                                                WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);";

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceLimitRepository"/> class.
        /// </summary>
        public ResourceLimitRepository(NpgsqlDataSource dataSource, TelemetryClient? telemetryClient = null)
        {
            _dataSource = dataSource;
            _telemetryClient = telemetryClient;
        }

        /// <inheritdoc/>
        public async Task<bool> SetEmailTimeout(DateTime timeout)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setEmailTimeout);
            using TelemetryTracker tracker = new(_telemetryClient, pgcom);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, timeout);
            await pgcom.ExecuteNonQueryAsync();
            tracker.Track();

            return true;
        }
    }
}
