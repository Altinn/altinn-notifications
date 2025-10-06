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
}
