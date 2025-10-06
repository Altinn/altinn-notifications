using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
public class DeadDeliveryReportRepository(NpgsqlDataSource npgsqlDataSource) : IDeadDeliveryReportRepository
{
    private readonly NpgsqlDataSource _dataSource = npgsqlDataSource;
    private const string _addDeadDeliveryReport = "SELECT notifications.insertdeaddeliveryreport(@channel, @attemptcount, @deliveryreport, @resolved, @firstseen, @lastattempt)";
    private const string _getDeadDeliveryReport = "SELECT id, channel, attemptcount, deliveryreport, resolved, firstseen, lastattempt FROM notifications.deaddeliveryreports WHERE id = @id";

    /// <inheritdoc/>
    public async Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_addDeadDeliveryReport);

        pgcom.Parameters.AddWithValue("channel", NpgsqlDbType.Smallint, (short)report.Channel);
        pgcom.Parameters.AddWithValue("attemptcount", NpgsqlDbType.Integer, report.AttemptCount);
        pgcom.Parameters.AddWithValue("deliveryreport", NpgsqlDbType.Jsonb, report.DeliveryReport);
        pgcom.Parameters.AddWithValue("resolved", NpgsqlDbType.Boolean, report.Resolved);
        pgcom.Parameters.AddWithValue("firstseen", NpgsqlDbType.TimestampTz, report.FirstSeen);
        pgcom.Parameters.AddWithValue("lastattempt", NpgsqlDbType.TimestampTz, report.LastAttempt);

        var result = await pgcom.ExecuteScalarAsync(cancellationToken);
        return result is null
            ? throw new InvalidOperationException("Database function insertdeaddeliveryreport returned null.")
            : (long)result;
    }

    /// <inheritdoc/>
    public async Task<DeadDeliveryReport> GetDeadDeliveryReportAsync(long id, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getDeadDeliveryReport);
        pgcom.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return new DeadDeliveryReport
            {
                Channel = (Core.Enums.DeliveryReportChannel)reader.GetInt16(1),
                AttemptCount = reader.GetInt32(2),
                DeliveryReport = reader.GetString(3),
                Resolved = reader.GetBoolean(4),
                FirstSeen = reader.GetDateTime(5),
                LastAttempt = reader.GetDateTime(6)
            };
        }
        else
        {
            throw new KeyNotFoundException($"DeadDeliveryReport with ID {id} not found.");
        }
    }
}
