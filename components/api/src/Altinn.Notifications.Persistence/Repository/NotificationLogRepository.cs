using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Models.NotificationLog;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public class NotificationLogRepository(NpgsqlDataSource dataSource) : ITransactionalNotificationLogRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentid := @shipmentId,
            _type := @notificationType,
            _orderchainid := @orderChainId,
            _dialogid := @dialogId,
            _transmissionid := @transmissionId,
            _operationid := @operationId,
            _gatewayreference := @gatewayReference,
            _recipient := @recipient,
            _destination := @destination,
            _resource := @resource,
            _status := @status,
            _sent_timestamp := @sentTimestamp
        )";

    /// <inheritdoc/>
    public async Task<long> InsertAsync(NotificationLogEntry entry)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await ExecuteInsertAsync(entry, connection, transaction: null);
    }

    /// <inheritdoc/>
    public async Task<long> InsertAsync(NotificationLogEntry entry, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        return await ExecuteInsertAsync(entry, connection, transaction);
    }

    private static async Task<long> ExecuteInsertAsync(
        NotificationLogEntry entry,
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@shipmentId", entry.ShipmentId);
        command.Parameters.AddWithValue("@notificationType", entry.NotificationType);
        command.Parameters.AddWithValue("@orderChainId", entry.OrderChainId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@dialogId", entry.DialogId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@transmissionId", entry.TransmissionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@operationId", entry.OperationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@gatewayReference", entry.GatewayReference ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@recipient", entry.Recipient ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@destination", entry.Destination ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@resource", entry.Resource ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@status", entry.Status ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@sentTimestamp", entry.SentTimestamp ?? (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return (long)result!;
    }
}
