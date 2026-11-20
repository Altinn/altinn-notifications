using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc />
public sealed class DynamicConfigRepository(NpgsqlDataSource dataSource) : IDynamicConfigRepository
{
    /// <inheritdoc />
    public async Task<int> GetPodCount(Guid podId, TimeSpan heartbeatTimeout, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT notifications.get_pod_count(@pod_id, @heartbeat_timeout)");
        command.Parameters.AddWithValue("pod_id", NpgsqlDbType.Uuid, podId);
        command.Parameters.AddWithValue("heartbeat_timeout", NpgsqlDbType.Interval, heartbeatTimeout);

        return await command.ExecuteScalarAsync(cancellationToken) is int count && count > 0
            ? count
            : throw new InvalidOperationException("The active pod count must include the current pod.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetConfig(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT key, value FROM notifications.get_config()");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            config.Add(reader.GetString(0), reader.GetString(1));
        }

        return config;
    }
}
