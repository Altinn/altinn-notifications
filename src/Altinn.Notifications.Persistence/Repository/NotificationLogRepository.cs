using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Persistence;
using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public class NotificationLogRepository(NpgsqlDataSource dataSource) : INotificationLogRepository
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
    public async Task<long> InsertAsync(
        Guid shipmentId,
        string notificationType,
        long? orderChainId = null,
        Guid? dialogId = null,
        string? transmissionId = null,
        string? operationId = null,
        string? gatewayReference = null,
        string? recipient = null,
        string? destination = null,
        string? resource = null,
        string? status = null,
        DateTime? sentTimestamp = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection);
        
        command.Parameters.AddWithValue("@shipmentId", shipmentId);
        command.Parameters.AddWithValue("@notificationType", notificationType);
        command.Parameters.AddWithValue("@orderChainId", orderChainId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@dialogId", dialogId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@transmissionId", transmissionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@operationId", operationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@gatewayReference", gatewayReference ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@recipient", recipient ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@destination", destination ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@resource", resource ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@status", status ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@sentTimestamp", sentTimestamp ?? (object)DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return (long)result!;
    }
}
