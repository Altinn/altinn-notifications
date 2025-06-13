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

    private static async Task ReadRecipients(List<Recipient> recipients, NpgsqlDataReader reader)
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
