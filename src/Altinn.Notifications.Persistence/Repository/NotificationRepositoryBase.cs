using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Persistence.Mappers;

using Microsoft.Extensions.Logging;
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

    private const string _getShipmentForStatusFeedSql = "SELECT * FROM notifications.getshipmentforstatusfeed(@alternateid)";

    private readonly ILogger _logger;

    private const string _insertStatusFeedEntrySql = @"SELECT notifications.insertstatusfeed(o._id, o.creatorname, @orderstatus)
                                                       FROM notifications.orders o
                                                       WHERE o.alternateid = @alternateid;";

    /// <summary>
    /// Constructor for the NotificationRepositoryBase class.
    /// </summary>
    /// <param name="logger">The logger associated with the above implementation</param>
    protected NotificationRepositoryBase(ILogger logger)
    {
        _logger = logger;
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
    /// </summary>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the alternate ID returned from the database cannot be parsed as a valid <see cref="Guid"/>.</exception>
    public async Task TerminateHangingNotifications()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            List<Guid> expiredIds = [];
            await using NpgsqlCommand pgcom = new(UpdateNotificationStatusSql, connection, transaction);
            pgcom.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, 10);

            // Use ExecuteReaderAsync since the RETURNING clause provides a result set.
            await using var reader = await pgcom.ExecuteReaderAsync();

            // Loop through the results as long as there are rows to read.
            while (await reader.ReadAsync())
            {
                // Read the value from the first column (index 0) of the result set
                // and add it to our list.
                // Assuming alternateid is a string. Use GetGuid(), GetInt32(), etc., if it's another type.
                var alternateId = reader.GetGuid(0);

                var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(alternateId, connection, transaction);

                if (orderIsSetAsCompleted)
                {
                    var orderStatus = await GetShipmentTracking(alternateId, connection, transaction);
                    if (orderStatus != null)
                    {
                        await InsertStatusFeed(orderStatus, connection, transaction);
                    }
                    else
                    {
                        // order status could not be retrieved, but we still commit the transaction to update the email notification, and order status
                        _logger.LogError("Order status could not be retrieved for the specified alternate ID.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while terminating hanging notifications.");
            await transaction.RollbackAsync();
            throw;
        }

        await transaction.CommitAsync();
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
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notificationId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, SourceIdentifier);

        var result = await pgcom.ExecuteScalarAsync();
        return result != null && (bool)result;
    }

    private async Task ReadRecipients(List<Recipient> recipients, NpgsqlDataReader reader)
    {
        while (await reader.ReadAsync())
        {
            string destination = await reader.GetFieldValueAsync<string>("destination");
            string status = await reader.GetFieldValueAsync<string>("status");
            var recipient = new Recipient
            {
                Destination = destination,
                Status = _mobileNumberRegex.IsMatch(destination) ? ProcessingLifecycleMapper.GetSmsLifecycleStage(status) : ProcessingLifecycleMapper.GetEmailLifecycleStage(status),
                LastUpdate = await reader.GetFieldValueAsync<DateTime>("last_update"),
            };
            recipients.Add(recipient);
        }
    }

    private static readonly Regex _mobileNumberRegex = Utilities.Helpers.MobileNumbersRegex();

    private static async Task<OrderStatus?> ReadMainNotification(NpgsqlDataReader reader)
    {
        var orderStatus = new OrderStatus
        {
            LastUpdated = await reader.GetFieldValueAsync<DateTime>("last_update"),
            ShipmentType = await reader.GetFieldValueAsync<string>("type"),
            ShipmentId = await reader.GetFieldValueAsync<Guid>("alternateid"),
            SendersReference = await reader.GetFieldValueAsync<string>("reference"),
            Status = ProcessingLifecycleMapper.GetOrderLifecycleStage(await reader.GetFieldValueAsync<string>("status")),
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
}
