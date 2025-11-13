using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Persistence.Mappers;

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

    private readonly string _updateExpiredNotifications = "SELECT * FROM notifications.updateexpirednotifications(@source, @limit, @offset)";
    private const string _getShipmentForStatusFeedSql = "SELECT * FROM notifications.getshipmentforstatusfeed_v2(@alternateid)";
    private const string _tryMarkOrderAsCompletedSql = "SELECT notifications.trymarkorderascompleted(@notificationid, @sourceidentifier)";

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
        await ReadRecipients(recipients, reader);

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
            try
            {
                await StatusFeedRepository.InsertStatusFeedEntry(orderStatus, connection, transaction);
            }
            catch (Exception ex)
            {
                var maskedAlternateId = string.Concat(alternateId.ToString().AsSpan(0, 8), "****");
                _logger.LogWarning(ex, "Failed to insert status feed for completed order {AlternateId}.", maskedAlternateId);
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

    private static async Task ReadRecipients(List<Recipient> recipients, NpgsqlDataReader reader)
    {
        while (await reader.ReadAsync())
        {
            var status = await reader.GetFieldValueAsync<string>("status");
            var destination = await reader.GetFieldValueAsync<string>("destination");
            var isValidMobileNumber = MobileNumberHelper.IsValidMobileNumber(destination);

            var recipient = new Recipient
            {
                Destination = destination,
                LastUpdate = await reader.GetFieldValueAsync<DateTime>("last_update"),
                Status = isValidMobileNumber ? ProcessingLifecycleMapper.GetSmsLifecycleStage(status) : ProcessingLifecycleMapper.GetEmailLifecycleStage(status)
            };

            recipients.Add(recipient);
        }
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
    /// Validates the result of an update operation and throws appropriate exceptions if the update failed.
    /// </summary>
    /// <param name="resultAlternateId">The alternate ID returned from the update operation, or null if not found.</param>
    /// <param name="isExpired">Indicates whether the notification has expired.</param>
    /// <param name="identifier">The identifier value used for the update.</param>
    /// <param name="identifierType">The type of identifier used (NotificationId, OperationId, or GatewayReference).</param>
    /// <param name="channel">The notification channel (Email or SMS).</param>
    /// <exception cref="Core.Exceptions.NotificationNotFoundException">Thrown when the notification is not found in the database.</exception>
    /// <exception cref="Core.Exceptions.NotificationExpiredException">Thrown when the notification has passed its expiry time (TTL).</exception>
    protected static void HandleUpdateResult(
        Guid? resultAlternateId,
        bool isExpired,
        string identifier,
        Core.Enums.SendStatusIdentifierType identifierType,
        Core.Enums.NotificationChannel channel)
    {
        // Notification not found in database
        if (resultAlternateId == null)
        {
            throw new Core.Exceptions.NotificationNotFoundException(channel, identifier, identifierType);
        }

        // Notification has passed its expiry time (TTL) - update was blocked
        if (isExpired)
        {
            throw new Core.Exceptions.NotificationExpiredException(channel, identifier, identifierType);
        }
    }

    /// <summary>
    /// Executes a notification status update within a transaction, handling result validation and order completion.
    /// This method provides a common template for updating notification status across different notification types.
    /// </summary>
    /// <remarks>
    /// The SQL function called via <paramref name="sqlCommand"/> MUST return a table with exactly three columns in this order:
    /// <list type="number">
    /// <item><description>Column 0: alternateid (uuid) - The notification's alternate ID, or NULL if not found</description></item>
    /// <item><description>Column 1: was_updated (boolean) - True if the update was performed, false otherwise</description></item>
    /// <item><description>Column 2: is_expired (boolean) - True if the notification has passed its expiry time</description></item>
    /// </list>
    ///
    /// Identifier precedence for error reporting mirrors SQL function behavior:
    /// If notificationId is provided (non-null and non-empty), it takes precedence for error messages.
    /// Otherwise, the secondaryIdentifier is used.
    /// </remarks>
    /// <param name="sqlCommand">The SQL command text to execute (e.g., "select * from notifications.updateemailnotification($1, $2, $3)").</param>
    /// <param name="parameters">Action to configure the command parameters before execution.</param>
    /// <param name="channel">The notification channel (Email or SMS).</param>
    /// <param name="notificationId">The notification ID (takes precedence for error reporting if provided).</param>
    /// <param name="secondaryIdentifier">The secondary identifier (operationId or gatewayReference).</param>
    /// <param name="secondaryIdentifierType">The type of the secondary identifier (OperationId or GatewayReference).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Core.Exceptions.NotificationNotFoundException">Thrown when the notification is not found in the database (alternateid is NULL).</exception>
    /// <exception cref="Core.Exceptions.NotificationExpiredException">Thrown when the notification has passed its expiry time (is_expired is true).</exception>
    protected async Task ExecuteUpdateWithTransactionAsync(
        string sqlCommand,
        Action<NpgsqlCommand> parameters,
        Core.Enums.NotificationChannel channel,
        Guid? notificationId,
        string? secondaryIdentifier,
        Core.Enums.SendStatusIdentifierType secondaryIdentifierType)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand pgcom = new(sqlCommand, connection, transaction);
            parameters(pgcom);

            // Execute and read the result table
            Guid? resultAlternateId = null;
            bool wasUpdated = false;
            bool isExpired = false;

            await using (var reader = await pgcom.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    resultAlternateId = await reader.IsDBNullAsync(0) ? null : reader.GetGuid(0);
                    wasUpdated = reader.GetBoolean(1);
                    isExpired = reader.GetBoolean(2);
                }
            }

            // Determine which identifier to use for error reporting (mirror SQL precedence)
            // NotificationId takes priority if provided (non-null and non-empty)
            var (identifierForError, identifierTypeForError) = notificationId.HasValue && notificationId != Guid.Empty
                ? (notificationId.Value.ToString(), Core.Enums.SendStatusIdentifierType.NotificationId)
                : (secondaryIdentifier!, secondaryIdentifierType);

            // Handle not found or expired cases
            HandleUpdateResult(resultAlternateId, isExpired, identifierForError, identifierTypeForError, channel);

            // Proceed with order completion logic if update was successful
            if (wasUpdated && resultAlternateId.HasValue)
            {
                var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(resultAlternateId.Value, connection, transaction);

                if (orderIsSetAsCompleted)
                {
                    await InsertOrderStatusCompletedOrder(connection, transaction, resultAlternateId.Value);
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
}
