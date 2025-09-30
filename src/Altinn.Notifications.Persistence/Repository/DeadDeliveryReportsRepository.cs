using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
public class DeadDeliveryReportsRepository(NpgsqlDataSource npgsqlDataSource) : IDeadDeliveryReportRepository
{
    private readonly NpgsqlDataSource _dataSource = npgsqlDataSource;
    private const string _addDeadDeliveryReport = "placeholder";

    /// <inheritdoc/>
    public async Task Add(DeadDeliveryReport report)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_addDeadDeliveryReport);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, report.Channel);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, report.AttemptCount);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, report.DeliveryReport);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Boolean, report.Resolved);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, report.FirstSeen);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, report.LastAttempt);

        await pgcom.ExecuteNonQueryAsync();
    }
}
