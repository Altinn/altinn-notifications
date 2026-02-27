using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Implementation of the <see cref="IResourceLimitRepository"/> interface
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ResourceLimitRepository"/> class.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class ResourceLimitRepository(NpgsqlDataSource dataSource) : IResourceLimitRepository
    {
        private readonly NpgsqlDataSource _dataSource = dataSource;
        private const string _setEmailTimeout = @"SELECT notifications.set_email_timeout(@timestamp);";

        /// <inheritdoc/>
        public async Task<bool> SetEmailTimeout(DateTime timeout)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setEmailTimeout);
            pgcom.Parameters.AddWithValue("timestamp", NpgsqlDbType.TimestampTz, timeout);
            var result = await pgcom.ExecuteScalarAsync();
            return result is bool success && success;
        }
    }
}
