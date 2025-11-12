using System.Data;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
public class DeadDeliveryReportRepository(NpgsqlDataSource npgsqlDataSource) : IDeadDeliveryReportRepository
{
    private readonly NpgsqlDataSource _dataSource = npgsqlDataSource;
    private const string _addDeadDeliveryReport = "SELECT notifications.insertdeaddeliveryreport(@channel, @attemptcount, @deliveryreport, @resolved, @firstseen, @lastattempt, @reason, @message)";
    private const string _getDeadDeliveryReport = "SELECT id, channel, attemptcount, deliveryreport, resolved, firstseen, lastattempt, reason, message FROM notifications.deaddeliveryreports WHERE id = @id";

    /// <inheritdoc/>
    public async Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_addDeadDeliveryReport);

        pgcom.Parameters.AddWithValue("channel", NpgsqlDbType.Smallint, (short)report.Channel);
        pgcom.Parameters.AddWithValue("attemptcount", NpgsqlDbType.Integer, report.AttemptCount);
        pgcom.Parameters.AddWithValue("deliveryreport", NpgsqlDbType.Jsonb, report.DeliveryReport);
        pgcom.Parameters.AddWithValue("resolved", NpgsqlDbType.Boolean, report.Resolved);
        pgcom.Parameters.AddWithValue("firstseen", NpgsqlDbType.TimestampTz, report.FirstSeen);
        pgcom.Parameters.AddWithValue("lastattempt", NpgsqlDbType.TimestampTz, report.LastAttempt);
        pgcom.Parameters.AddWithValue("reason", NpgsqlDbType.Text, report.Reason ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("message", NpgsqlDbType.Text, report.Message ?? (object)DBNull.Value);

        var result = await pgcom.ExecuteScalarAsync(cancellationToken);
        return result is null
            ? throw new InvalidOperationException("Database function insertdeaddeliveryreport returned null.")
            : (long)result;
    }

    /// <inheritdoc/>
    public async Task<DeadDeliveryReport> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getDeadDeliveryReport);
        pgcom.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var reasonOrdinal = reader.GetOrdinal("reason");
            var messageOrdinal = reader.GetOrdinal("message");

            return new DeadDeliveryReport
            {
                Channel = (Core.Enums.DeliveryReportChannel)await reader.GetFieldValueAsync<short>("channel", cancellationToken),
                AttemptCount = await reader.GetFieldValueAsync<int>("attemptcount", cancellationToken),
                DeliveryReport = await reader.GetFieldValueAsync<string>("deliveryreport", cancellationToken),
                Resolved = await reader.GetFieldValueAsync<bool>("resolved", cancellationToken),
                FirstSeen = await reader.GetFieldValueAsync<DateTime>("firstseen", cancellationToken),
                LastAttempt = await reader.GetFieldValueAsync<DateTime>("lastattempt", cancellationToken),
                Reason = await reader.IsDBNullAsync(reasonOrdinal, cancellationToken) ? null : await reader.GetFieldValueAsync<string>(reasonOrdinal, cancellationToken),
                Message = await reader.IsDBNullAsync(messageOrdinal, cancellationToken) ? null : await reader.GetFieldValueAsync<string>(messageOrdinal, cancellationToken)
            };
        }
        else
        {
            throw new KeyNotFoundException($"DeadDeliveryReport with ID {id} not found.");
        }
    }
}
