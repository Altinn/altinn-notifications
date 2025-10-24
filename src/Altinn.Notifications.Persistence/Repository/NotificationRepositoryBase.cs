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
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Gets the unique identifier for the source associated with the derived class e.g. sms or email.
    /// </summary>
    protected abstract string SourceIdentifier { get; }

    private readonly string _updateExpiredNotifications = "SELECT * FROM notifications.updateexpirednotifications(@source, @limit)";
    private const string _getShipmentForStatusFeedSql = "SELECT * FROM notifications.getshipmentforstatusfeed_v2(@alternateid)";
    private const string _tryMarkOrderAsCompletedSql = "SELECT notifications.trymarkorderascompleted(@notificationid, @sourceidentifier)";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger _logger;
    private readonly int _terminationBatchSize;

    private const string _insertStatusFeedEntrySql = @"SELECT notifications.insertstatusfeed(o._id, o.creatorname, @orderstatus)
                                                       FROM notifications.orders o
                                                       WHERE o.alternateid = @alternateid;";

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
            _logger.LogWarning("No shipment tracking information found for alternate ID {AlternateId}.", notificationAlternateId);
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
            await InsertStatusFeed(orderStatus, connection, transaction);
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
    /// Inserts a new status feed entry for an order.
    /// </summary>
    /// <param name="orderStatus">The status object that should be serialized as jsonb</param>
    /// <param name="connection">The connection used with this transaction</param>
    /// <param name="transaction">The transaction used with this transaction enclosing order status update</param>
    /// <returns>No return value</returns>
    protected async Task InsertStatusFeed(OrderStatus orderStatus, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand pgcom = new(_insertStatusFeedEntrySql, connection, transaction);
        pgcom.Parameters.AddWithValue("alternateid", NpgsqlDbType.Uuid, orderStatus.ShipmentId);
        pgcom.Parameters.AddWithValue("orderstatus", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(orderStatus, _serializerOptions));
        await pgcom.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Validates the result of an update operation and throws appropriate exceptions if the update failed.
    /// </summary>
    /// <param name="resultAlternateId">The alternate ID returned from the update operation, or null if not found.</param>
    /// <param name="isExpired">Indicates whether the notification has expired.</param>
    /// <param name="hasIdentifier">Indicates whether an identifier (operationId or gatewayReference) was provided.</param>
    /// <param name="identifier">The identifier value (operationId or gatewayReference).</param>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="channel">The notification channel (Email or SMS).</param>
    /// <param name="identifierType">The type of identifier when hasIdentifier is true (OperationId or GatewayReference).</param>
    /// <exception cref="Core.Exceptions.NotificationNotFoundException">Thrown when the notification is not found in the database.</exception>
    /// <exception cref="Core.Exceptions.NotificationExpiredException">Thrown when the notification has passed its expiry time (TTL).</exception>
    protected static void HandleUpdateResult(
        Guid? resultAlternateId,
        bool isExpired,
        bool hasIdentifier,
        string? identifier,
        Guid? notificationId,
        Core.Enums.NotificationChannel channel,
        Core.Enums.SendStatusIdentifierType identifierType)
    {
        // Notification not found in database
        if (resultAlternateId == null)
        {
            var id = hasIdentifier ? identifier! : notificationId!.Value.ToString();
            var idType = hasIdentifier ? identifierType : Core.Enums.SendStatusIdentifierType.NotificationId;
            throw new Core.Exceptions.NotificationNotFoundException(channel, id, idType);
        }

        // Notification has passed its expiry time (TTL) - update was blocked
        if (isExpired)
        {
            var id = hasIdentifier ? identifier! : notificationId!.Value.ToString();
            var idType = hasIdentifier ? identifierType : Core.Enums.SendStatusIdentifierType.NotificationId;
            throw new Core.Exceptions.NotificationExpiredException(channel, id, idType);
        }
    }
}
