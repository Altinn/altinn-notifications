using System.Collections.Immutable;
using System.Data;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Persistence.Mappers;
using Altinn.Notifications.Persistence.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Base class for notification repositories.
/// </summary>
public abstract class NotificationRepositoryBase
{
    /// <summary>
    /// Gets the unique identifier for the source associated with the derived class e.g. sms or email.
    /// </summary>
    protected abstract string SourceIdentifier { get; }

    /// <summary>
    /// Gets the notification channel handled by the derived class, used when reporting update failures.
    /// </summary>
    protected abstract NotificationChannel Channel { get; }

    private const string _getShipmentForStatusFeedSql = "SELECT * FROM notifications.getshipmentforstatusfeed_v3(@alternateid)";
    
    private const string _tryMarkOrderAsCompletedSql = "SELECT notifications.trymarkorderascompleted(@notificationid, @sourceidentifier)";

    private readonly string _updateExpiredNotifications = "SELECT * FROM notifications.updateexpirednotifications(@source, @limit, @offset)";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger _logger;
    private readonly int _terminationBatchSize;
    private readonly int _expiryOffsetSeconds;

    private const string _referenceColumnName = "reference";
    private const string _typeColumnName = "type";
    private const string _statusColumnName = "status";

    /// <summary>
    /// Constructor for the NotificationRepositoryBase class.
    /// </summary>
    /// <param name="dataSource">The datasource used to integrate with the database</param>
    /// <param name="logger">The logger associated with the above implementation</param>
    /// <param name="config">The notification configuration</param>
    protected NotificationRepositoryBase(NpgsqlDataSource dataSource, ILogger logger, IOptions<NotificationConfig> config)
    {
        _dataSource = dataSource;
        _logger = logger;
        _expiryOffsetSeconds = config.Value.ExpiryOffsetSeconds;

        if (config.Value.TerminationBatchSize > 0)
        {
            _terminationBatchSize = config.Value.TerminationBatchSize;
        }
        else
        {
            _logger.LogWarning("Invalid TerminationBatchSize configuration value {ConfiguredValue}. Using default value of 100.", config.Value.TerminationBatchSize);
            _terminationBatchSize = 100;
        }
    }

    /// <summary>
    /// Get shipment tracking information for a specific notification based on its alternate ID.
    /// </summary>
    /// <param name="notificationAlternateId">Guid for the email or sms notification alternate id</param>
    /// <param name="connection">The database connection to be used for the query execution</param>
    /// <param name="transaction">The database transaction to be used for the query execution.</param>
    /// <returns>Order status object if the order was found in the database. Otherwise, null</returns>
    protected async Task<OrderStatus?> GetShipmentTracking(Guid notificationAlternateId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand pgcom = new(_getShipmentForStatusFeedSql, connection, transaction);
        pgcom.Parameters.AddWithValue("alternateid", NpgsqlDbType.Uuid, notificationAlternateId);
        List<Recipient> recipients = [];

        await using var reader = await pgcom.ExecuteReaderAsync();
        var hasRows = await reader.ReadAsync();

        if (!hasRows)
        {
            var maskedAlternateId = string.Concat(notificationAlternateId.ToString().AsSpan(0, 8), "****");
            _logger.LogWarning("No shipment tracking information found for alternate ID {AlternateId}.", maskedAlternateId);
            return null; // Return null if no rows were found
        }

        // read main notification or reminder
        var orderStatus = await ReadMainNotification(reader);

        // Add recipients to the order status
        await NotificationUtil.ReadRecipientsAsync(recipients, reader, CancellationToken.None);

        if (orderStatus != null)
        {
            var updatedOrderStatus = orderStatus with { Recipients = recipients.ToImmutableList() };
            return updatedOrderStatus;
        }
        else
        {
            var maskedAlternateId = string.Concat(notificationAlternateId.ToString().AsSpan(0, 8), "****");
            _logger.LogWarning("No shipment tracking information found for alternate ID {AlternateId}.", maskedAlternateId);
            return null; // Return null if no order status was found
        }
    }

    /// <summary>
    /// Terminates hanging email/sms notifications by updating their status and attempting to complete associated orders.
    /// Updates rows in a configurable batch size per function call. All subsequent processing happens in the same transaction.
    /// All rows in the batch or nothing is committed to the database.
    /// </summary>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the alternate ID returned from the database cannot be parsed as a valid <see cref="Guid"/>.</exception>
    public async Task TerminateExpiredNotifications()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand pgcom = new(_updateExpiredNotifications, connection, transaction);
            pgcom.Parameters.AddWithValue("source", NpgsqlDbType.Text, SourceIdentifier); // Source identifier for the notifications
            pgcom.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, _terminationBatchSize);
            pgcom.Parameters.AddWithValue("offset", NpgsqlDbType.Integer, _expiryOffsetSeconds);

            await using var reader = await pgcom.ExecuteReaderAsync();

            // Buffer all IDs 
            var expiredIds = new List<Guid>(10);
            while (await reader.ReadAsync())
            {
                expiredIds.Add(await reader.GetFieldValueAsync<Guid>(0));
            }

            // close the reader to release the connection for the next operation
            await reader.CloseAsync();

            foreach (var alternateId in expiredIds)
            {
                if (await TryCompleteOrderBasedOnNotificationsState(alternateId, connection, transaction))
                {
                    await InsertOrderStatusCompletedOrder(connection, transaction, alternateId);
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Inserts the status feed for a completed order based on the specified alternate ID.
    /// </summary>
    /// <param name="connection">The <see cref="NpgsqlConnection"/> used to interact with the database.</param>
    /// <param name="transaction">The <see cref="NpgsqlTransaction"/> associated with the database operation.</param>
    /// <param name="alternateId">The unique identifier for the order, used to retrieve its status.</param>
    protected async Task InsertOrderStatusCompletedOrder(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid alternateId)
    {
        var orderStatus = await GetShipmentTracking(alternateId, connection, transaction);
        if (orderStatus != null)
        {
            bool inserted = await StatusFeedRepository.InsertStatusFeedEntry(orderStatus, connection, transaction);
            if (!inserted)
            {
                _logger.LogError("Status feed entry for completed order {AlternateId} already existed; this order was processed more than once.", alternateId);
            }

            var skippedNotificationIds = await NotificationLogRepository.InsertNotificationLogEntry(orderStatus.ShipmentId, connection, transaction);
            foreach (var skippedNotificationId in skippedNotificationIds)
            {
                _logger.LogError("Notification log entry for notification {NotificationId} (shipment {AlternateId}) already existed; this notification was processed more than once.", skippedNotificationId, alternateId);
            }
        }
        else
        {
            _logger.LogError("Order status could not be retrieved for the specified alternate ID.");
            throw new InvalidOperationException("Order status could not be retrieved for the specified alternate ID.");
        }
    }

    /// <summary>
    /// Attempts to mark an order as completed based on the state of a notification.
    /// </summary>
    /// <remarks>This method executes a database command to update the order's state based on the provided
    /// notification ID. Ensure that the <paramref name="connection"/> is open and the <paramref name="transaction"/> is
    /// active before calling this method.</remarks>
    /// <param name="notificationId">The unique identifier of the notification associated with the order.</param>
    /// <param name="connection">The open <see cref="NpgsqlConnection"/> to the database.</param>
    /// <param name="transaction">The active <see cref="NpgsqlTransaction"/> to use for the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the order was
    /// successfully marked as completed; otherwise, <see langword="false"/>.</returns>
    protected async Task<bool> TryCompleteOrderBasedOnNotificationsState(Guid notificationId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand pgcom = new(_tryMarkOrderAsCompletedSql, connection, transaction);
        pgcom.Parameters.AddWithValue("notificationid", NpgsqlDbType.Uuid, notificationId);
        pgcom.Parameters.AddWithValue("sourceidentifier", NpgsqlDbType.Text, SourceIdentifier);

        var result = await pgcom.ExecuteScalarAsync();
        return result != null && (bool)result;
    }

    private static async Task<OrderStatus?> ReadMainNotification(NpgsqlDataReader reader)
    {
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        var typeOrdinal = reader.GetOrdinal(_typeColumnName);
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);

        var status = await reader.IsDBNullAsync(statusOrdinal) ? string.Empty : await reader.GetFieldValueAsync<string>(statusOrdinal);

        var orderStatus = new OrderStatus
        {
            LastUpdated = await reader.GetFieldValueAsync<DateTime>("last_update"),
            ShipmentType = await reader.IsDBNullAsync(typeOrdinal) ? null : await reader.GetFieldValueAsync<string>(_typeColumnName),
            ShipmentId = await reader.GetFieldValueAsync<Guid>("alternateid"),
            SendersReference = await reader.IsDBNullAsync(referenceOrdinal) ? null : await reader.GetFieldValueAsync<string?>(_referenceColumnName),
            Status = ProcessingLifecycleMapper.GetOrderLifecycleStage(status),
            Recipients = [] // Initialize with an empty immutable list
        };

        return orderStatus;
    }

    /// <summary>
    /// Executes a notification status update within the caller's unit of work, handling result validation and order completion.
    /// </summary>
    /// <remarks>
    /// The SQL function called via <paramref name="sqlCommand"/> MUST return a table with exactly three columns in this order:
    /// <list type="number">
    /// <item><description>Column 0: alternateid (uuid) - The notification's alternate ID, or NULL if not found</description></item>
    /// <item><description>Column 1: was_updated (boolean) - True if the update was performed, false otherwise</description></item>
    /// <item><description>Column 2: is_expired (boolean) - True if the notification has passed its expiry time</description></item>
    /// </list>
    ///
    /// The method never commits or rolls back. The caller owns the unit of work and is responsible for both.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work that provides the active connection and transaction.</param>
    /// <param name="sqlCommand">The SQL command text to execute.</param>
    /// <param name="parameters">Action to configure the command parameters before execution.</param>
    /// <param name="identifier">The identifier reported if the update fails. Callers should mirror the SQL function's lookup precedence.</param>
    /// <param name="identifierType">The type of <paramref name="identifier"/>.</param>
    /// <param name="isIntermediateStatus">Whether the new status is an intermediate one that should skip order completion.</param>
    /// <param name="cancellationToken">A token used for cancelling the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotificationNotFoundException">Thrown when no notification matches the given identifiers.</exception>
    /// <exception cref="NotificationExpiredException">Thrown when the notification has passed its expiry time (is_expired is true).</exception>
    protected async Task ExecuteUpdateAsync(
        Core.Models.UnitOfWork unitOfWork,
        string sqlCommand,
        Action<NpgsqlCommand> parameters,
        string identifier,
        SendStatusIdentifierType identifierType,
        bool isIntermediateStatus,
        CancellationToken cancellationToken)
    {
        var connection = unitOfWork.Connection;
        var transaction = unitOfWork.Transaction;

        await using NpgsqlCommand pgcom = new(sqlCommand, connection, transaction);
        parameters(pgcom);

        Guid? resultAlternateId = null;
        bool wasUpdated = false;
        bool isExpired = false;

        await using (var reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                resultAlternateId = await reader.IsDBNullAsync(0, cancellationToken) ? null : reader.GetGuid(0);
                wasUpdated = reader.GetBoolean(1);
                isExpired = reader.GetBoolean(2);
            }
        }

        // Notification not found in database
        if (resultAlternateId is not Guid alternateId)
        {
            throw new NotificationNotFoundException(Channel, identifier, identifierType);
        }

        // Notification has passed its expiry time (TTL) - update was blocked
        if (isExpired)
        {
            throw new NotificationExpiredException(Channel, identifier, identifierType);
        }

        // Skip order completion when nothing was updated or the status is intermediate (Succeeded, Accepted)
        if (!wasUpdated || isIntermediateStatus)
        {
            return;
        }

        var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(
            alternateId,
            connection,
            transaction);

        if (!orderIsSetAsCompleted)
        {
            return;
        }

        await InsertOrderStatusCompletedOrder(connection, transaction, alternateId);
    }
}
