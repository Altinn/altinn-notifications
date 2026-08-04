using System.Collections.Immutable;

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
            notificationid,
            dialogid,
            transmissionid,
            type,
            channel,
            destination,
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
    public async Task<IImmutableList<NotificationLogSummary>> GetByDialogId(string dialogId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialogId);

        await using var command = CreateNotificationLogCommand(dialogId, null);

        return await ReadNotificationLogSummaries(command, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IImmutableList<NotificationLogSummary>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transmissionId);

        await using var command = CreateNotificationLogCommand(null, transmissionId);

        return await ReadNotificationLogSummaries(command, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IImmutableList<NotificationLogSummary>> GetByDialogAndTransmission(string dialogId, string transmissionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(transmissionId);

        await using var command = CreateNotificationLogCommand(dialogId, transmissionId);

        return await ReadNotificationLogSummaries(command, cancellationToken);
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
    /// Maps the current row of the database reader to a <see cref="NotificationLogSummary"/>.
    /// </summary>
    /// <param name="reader">The database reader positioned on a notification log row.</param>
    /// <returns>The notification log summary represented by the current row.</returns>
    private static NotificationLogSummary MapNotificationLogSummary(NpgsqlDataReader reader)
    {
        return new NotificationLogSummary
        {
            NotificationId = reader.GetGuid(0),
            DialogId = GetNullableString(reader, 1),
            TransmissionId = GetNullableString(reader, 2),
            Type = reader.GetString(3),
            Channel = reader.GetString(4),
            Destination = reader.GetString(5),
            Status = reader.GetString(6),
            RequestedSendTime = reader.GetDateTime(7),
            LastUpdateTime = reader.GetDateTime(8)
        };
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
    /// Executes the specified command and maps the returned rows to notification log summaries.
    /// </summary>
    /// <param name="command">The database command to execute.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>An immutable collection of notification log summaries.</returns>
    private static async Task<IImmutableList<NotificationLogSummary>> ReadNotificationLogSummaries(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<NotificationLogSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapNotificationLogSummary(reader));
        }

        return result.ToImmutableList();
    }
}
