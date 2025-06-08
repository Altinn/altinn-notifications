using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Persistence.Mappers;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Base class for notification repositories.
/// </summary>
public class NotificationRepositoryBase
{
    private JsonSerializerOptions _serializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private NpgsqlDataSource _dataSource;
    private ILogger _logger;
    private const string _getShipmentTrackingEmailSql = @"SELECT notifications.get_shipment_tracking_v2(o.alternateid, o.creatorname), o.alternateid
                                                         FROM notifications.orders o
                                                         INNER JOIN notifications.emailnotifications e ON e._orderid = o._id
                                                         WHERE e.alternateid = @notificationalternateid";
    
    private const string _getShipmentTrackingSmsSql = @"SELECT notifications.get_shipment_tracking_v2(o.alternateid, o.creatorname), o.alternateid
                                                         FROM notifications.orders o
                                                         INNER JOIN notifications.smsnotifications e ON e._orderid = o._id
                                                         WHERE e.alternateid = @notificationalternateid";
    
    private const string _insertStatusFeedEntrySql = @"SELECT notifications.insertstatusfeed(o._id, o.creatorname, @orderstatus)
                                   FROM notifications.orders o
                                   WHERE o.alternateid = @alternateid;"; // This is a test query, not used in production

    /// <summary>
    /// Constructor for the NotificationRepositoryBase class.
    /// </summary>
    /// <param name="dataSource">The datasource inherited from above</param>
    /// <param name="logger">The logger associated with the above implementation</param>
    public NotificationRepositoryBase(NpgsqlDataSource dataSource, ILogger logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    /// <summary>
    /// Get shipment tracking information for a specific notification based on its alternate ID.
    /// </summary>
    /// <param name="notificationAlternateId">Guid for the email or sms notification alternate id</param>
    /// <returns>Order status object if the order was found in the database. Otherwise, null</returns>
    public async Task<OrderStatus?> GetShipmentTracking(Guid notificationAlternateId)
    {
        string shipmentTrackingSql = this switch
        {
            EmailNotificationRepository => _getShipmentTrackingEmailSql,
            SmsNotificationRepository => _getShipmentTrackingSmsSql,
            _ => throw new NotSupportedException($"Unsupported repository type: {this.GetType().Name}")
        };

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(shipmentTrackingSql);
        pgcom.Parameters.AddWithValue("notificationalternateid", NpgsqlDbType.Uuid, notificationAlternateId);
        OrderStatus? orderStatus = null; 
        List<Recipient> recipients = [];

        await using var reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        // read main notification or reminder
        if (reader.FieldCount == 2 && reader.GetDataTypeName(0) == "record")
        {
            var alternateId = reader.GetFieldValue<Guid>(1);

            var (sendersReference, status, lastUpdated, destination, type) = reader.GetFieldValue<(string, string, DateTime, string, string)>(0);
            orderStatus = new OrderStatus
            {
                LastUpdated = lastUpdated,
                ShipmentType = type,
                ShipmentId = alternateId,
                SendersReference = sendersReference,
                Recipients = [] // Initialize with an empty immutable list
            };
        }

        // Add recipients to the order status
        while (await reader.ReadAsync())
        {
            if (reader.FieldCount == 2 && reader.GetDataTypeName(0) == "record")
            {
                var (sendersReference, status, lastUpdated, destination, type) = reader.GetFieldValue<(string, string, DateTime, string, string)>(0);

                var recipient = new Recipient
                {
                    Destination = destination,
                    Status = Utilities.Helpers.MobileNumbersRegex().IsMatch(destination) ? ProcessingLifecycleMapper.GetSmsLifecycleStage(status) : ProcessingLifecycleMapper.GetEmailLifecycleStage(status),
                    LastUpdate = lastUpdated
                };

                recipients.Add(recipient);
            }
        }

        if (orderStatus != null) // Ensure orderStatus is not null before dereferencing
        {
            var updatedOrderStatus = orderStatus with { Recipients = recipients.ToImmutableList() };
            return updatedOrderStatus;
        }
        else
        {
            _logger.LogWarning("No shipment tracking information found for alternate ID {AlternateId}.", notificationAlternateId);
            return null; // Return null if no order status was found
        }
    }

    /// <summary>
    /// Inserts a new status feed entry for an order.
    /// </summary>
    /// <param name="orderStatus">The status object that should be serialized as jsonb</param>
    /// <returns>No return value</returns>
    public async Task InsertStatusFeed(OrderStatus orderStatus)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertStatusFeedEntrySql);
        pgcom.Parameters.AddWithValue("alternateid", NpgsqlDbType.Uuid, orderStatus.ShipmentId);
        pgcom.Parameters.AddWithValue("orderstatus", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(orderStatus, _serializerOptions));
        await pgcom.ExecuteNonQueryAsync();
    }
}
