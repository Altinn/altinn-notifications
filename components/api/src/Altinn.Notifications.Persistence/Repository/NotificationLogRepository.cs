using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Represents notification log operations.
/// </summary>
public sealed class NotificationLogRepository(NpgsqlDataSource dataSource) : INotificationLogRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private const string _getNotificationLogSql = @"
        SELECT
            orderchainid,
            shipmentid,
            notificationid,
            creatorname,
            sendersreference,
            dialogid,
            transmissionid,
            deliveryreference,
            recipient,
            type,
            channel,
            destination,
            resource,
            status,
            requestedsendtime,
            lastupdatetime
        FROM notifications.getnotificationlog(
            _dialogid := @dialogid,
            _transmissionid := @transmissionid
        )";

    private const string _insertNotificationLogSql = @"
        SELECT notifications.insert_notification_log(
            _shipmentId := @shipmentId
        )";

    /// <summary>
    /// Inserts notification log entries derived from the email/sms notifications for the specified shipment.
    /// </summary>
    /// <param name="shipmentId">The alternate ID of the order to insert notification log entries for.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The database transaction to use.</param>
    /// <returns>
    /// The notification ids that were skipped because a log entry already existed for them (idempotent no-op).
    /// Empty when every notification for the shipment was logged successfully.
    /// </returns>
    /// <remarks>
    /// This is a shared helper method that can be called from both NotificationRepositoryBase
    /// and OrderRepository to insert notification log entries consistently.
    /// </remarks>
    public static async Task<IReadOnlyList<Guid>> InsertNotificationLogEntry(Guid shipmentId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(_insertNotificationLogSql, connection, transaction);

        command.Parameters.AddWithValue("@shipmentId", shipmentId);

        var result = await command.ExecuteScalarAsync();

        return result is null or DBNull ? [] : (Guid[])result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationLogEntry>> GetByDialogOrTransmission(string? transmissionId, string? dialogId, CancellationToken cancellationToken)
    {
        await using var command = CreateNotificationLogCommand(dialogId, transmissionId);

        return await ReadNotificationLogEntries(command, cancellationToken);
    }

    /// <summary>
    /// Gets a nullable <see cref="Guid"/> from the specified column.
    /// </summary>
    /// <param name="reader">The database reader containing the value.</param>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <returns>
    /// The <see cref="Guid"/> value, or <see langword="null"/> if the column contains <see cref="DBNull"/>.
    /// </returns>
    private static Guid? GetNullableGuid(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    /// <summary>
    /// Gets a nullable string from the specified column.
    /// </summary>
    /// <param name="reader">The database reader containing the value.</param>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <returns>
    /// The string value, or <see langword="null"/> if the column contains <see cref="DBNull"/>.
    /// </returns>
    private static string? GetNullableString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Maps the current row of the database reader to a <see cref="NotificationLogEntry"/>.
    /// </summary>
    /// <param name="reader">The database reader positioned on a notification log row.</param>
    /// <returns>The notification log entry represented by the current row.</returns>
    private static NotificationLogEntry MapNotificationLogEntry(NpgsqlDataReader reader)
    {
        return new NotificationLogEntry(
            OrderChainId: GetNullableGuid(reader, 0),
            ShipmentId: reader.GetGuid(1),
            NotificationId: reader.GetGuid(2),
            CreatorName: reader.GetString(3),
            SendersReference: GetNullableString(reader, 4),
            DialogId: GetNullableString(reader, 5),
            TransmissionId: GetNullableString(reader, 6),
            DeliveryReference: GetNullableString(reader, 7),
            Recipient: GetNullableString(reader, 8),
            Type: reader.GetString(9),
            Channel: reader.GetString(10),
            Destination: reader.GetString(11),
            Resource: GetNullableString(reader, 12),
            Status: reader.GetString(13),
            RequestedSendTime: reader.GetDateTime(14),
            LastUpdateTime: reader.GetDateTime(15));
    }

    /// <summary>
    /// Creates a database command for retrieving notification log entries.
    /// </summary>
    /// <param name="dialogId">
    /// The Dialogporten dialog identifier to filter by, or <see langword="null"/>.
    /// </param>
    /// <param name="transmissionId">
    /// The Dialogporten transmission identifier to filter by, or <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A configured database command for retrieving notification log entries.
    /// </returns>
    private NpgsqlCommand CreateNotificationLogCommand(string? dialogId, string? transmissionId)
    {
        var command = _dataSource.CreateCommand(_getNotificationLogSql);

        command.Parameters.AddWithValue("dialogid", dialogId is null ? DBNull.Value : dialogId);

        command.Parameters.AddWithValue("transmissionid", transmissionId is null ? DBNull.Value : transmissionId);

        return command;
    }

    /// <summary>
    /// Executes the specified command and maps the returned rows to notification log entries.
    /// </summary>
    /// <param name="command">The database command to execute.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The notification log entries returned by the database.</returns>
    private static async Task<IReadOnlyList<NotificationLogEntry>> ReadNotificationLogEntries(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<NotificationLogEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapNotificationLogEntry(reader));
        }

        return result;
    }
}
