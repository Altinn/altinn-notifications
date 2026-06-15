using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Tools.DlqManager.Repositories;

/// <summary>
/// Npgsql-based implementation of <see cref="IOrderRepository"/>.
/// Only contains the two targeted queries needed by the DLQ Manager — deliberately
/// minimal to avoid coupling to the full Persistence component.
/// </summary>
public class OrderRepository(NpgsqlDataSource dataSource) : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    /// <inheritdoc/>
    public async Task<(string? Status, long NotificationCount, DateTime? ExpiryTime, bool IsExpired)> GetOrderStateAsync(Guid orderId)
    {
        const string sql = """
            SELECT
                o.processedstatus::text,
                COALESCE(
                    (SELECT COUNT(*) FROM notifications.smsnotifications   WHERE _orderid = o._id), 0
                ) + COALESCE(
                    (SELECT COUNT(*) FROM notifications.emailnotifications WHERE _orderid = o._id), 0
                ) AS total_notifications,
                o.requestedsendtime + INTERVAL '48 hours'                              AS expirytime,
                NOW() > o.requestedsendtime + INTERVAL '48 hours'                     AS isexpired
            FROM notifications.orders o
            WHERE o.alternateid = @orderId
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter("orderId", NpgsqlDbType.Uuid) { Value = orderId });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var status     = await reader.GetFieldValueAsync<string>(0);
            var count      = await reader.GetFieldValueAsync<long>(1);
            var expiryTime = DateTime.SpecifyKind(await reader.GetFieldValueAsync<DateTime>(2), DateTimeKind.Utc);
            var isExpired  = await reader.GetFieldValueAsync<bool>(3);
            return (status, count, expiryTime, isExpired);
        }

        return (null, 0, null, false);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, (string? Status, long NotificationCount, DateTime? ExpiryTime, bool IsExpired)>> GetOrderStatesAsync(
        IReadOnlyList<Guid> orderIds)
    {
        const string sql = """
            SELECT
                o.alternateid,
                o.processedstatus::text,
                COALESCE(
                    (SELECT COUNT(*) FROM notifications.smsnotifications   WHERE _orderid = o._id), 0
                ) + COALESCE(
                    (SELECT COUNT(*) FROM notifications.emailnotifications WHERE _orderid = o._id), 0
                ) AS total_notifications,
                o.requestedsendtime + INTERVAL '48 hours'                              AS expirytime,
                NOW() > o.requestedsendtime + INTERVAL '48 hours'                     AS isexpired
            FROM notifications.orders o
            WHERE o.alternateid = ANY(@orderIds)
            """;

        var result = new Dictionary<Guid, (string? Status, long NotificationCount, DateTime? ExpiryTime, bool IsExpired)>();

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("orderIds", orderIds.ToArray()));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id         = await reader.GetFieldValueAsync<Guid>(0);
            var status     = await reader.GetFieldValueAsync<string>(1);
            var count      = await reader.GetFieldValueAsync<long>(2);
            var expiryTime = DateTime.SpecifyKind(await reader.GetFieldValueAsync<DateTime>(3), DateTimeKind.Utc);
            var isExpired  = await reader.GetFieldValueAsync<bool>(4);
            result[id] = (status, count, expiryTime, isExpired);
        }

        return result;
    }
}
