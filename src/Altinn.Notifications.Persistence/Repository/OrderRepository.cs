using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Persistence.Extensions;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
[ExcludeFromCodeCoverage]
public class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _getOrderByIdSql = "select notificationorder from notifications.orders where alternateid=$1 and creatorname=$2";
    private const string _getOrdersBySendersReferenceSql = "select notificationorder from notifications.orders where sendersreference=$1 and creatorname=$2";
    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6, $7, $8)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder, _sendingtimepolicy, _type)
    private const string _insertEmailTextSql = "call notifications.insertemailtext($1, $2, $3, $4, $5)"; // (__orderid, _fromaddress, _subject, _body, _contenttype)
    private const string _insertSmsTextSql = "insert into notifications.smstexts(_orderid, sendernumber, body) VALUES ($1, $2, $3)"; // __orderid, _sendernumber, _body
    private const string _setProcessCompleted = "update notifications.orders set processedstatus =$1::orderprocessingstate where alternateid=$2";
    private const string _getOrdersPastSendTimeUpdateStatus = "select notifications.getorders_pastsendtime_updatestatus()";
    private const string _getOrderIncludeStatus = "select * from notifications.getorder_includestatus_v4($1, $2)"; // _alternateid,  creator
    private const string _cancelAndReturnOrder = "select * from notifications.cancelorder($1, $2)"; // _alternateid,  creator
    private const string _insertorderchainSql = "call notifications.insertorderchain($1, $2, $3, $4, $5)"; // (_orderid, _idempotencyid, _creatorname, _created, _orderchain)
    private const string _getOrdersChainTrackingSql = "SELECT * FROM notifications.get_orders_chain_tracking($1, $2)"; // (_creatorname, _idempotencyid)

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public OrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder?> GetOrderById(Guid id, string creator)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderByIdSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        NotificationOrder? order = null;

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                order = reader.GetFieldValue<NotificationOrder>("notificationorder");
            }
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task<List<NotificationOrder>> GetOrdersBySendersReference(string sendersReference, string creator)
    {
        List<NotificationOrder> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrdersBySendersReferenceSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, sendersReference);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                NotificationOrder notificationOrder = reader.GetFieldValue<NotificationOrder>("notificationorder");
                searchResult.Add(notificationOrder);
            }
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder> Create(NotificationOrder order)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            long dbOrderId = await InsertOrder(order, connection, transaction);

            EmailTemplate? emailTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Email) as EmailTemplate;
            await InsertEmailTextAsync(dbOrderId, emailTemplate, connection, transaction);

            SmsTemplate? smsTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
            await InsertSmsTextAsync(dbOrderId, smsTemplate, connection, transaction);

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task<List<NotificationOrder>> Create(NotificationOrderChainRequest orderChain, NotificationOrder mainOrder, List<NotificationOrder>? reminders, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InsertOrderChainAsync(orderChain, mainOrder.Created, connection, transaction, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            long mainOrderId = await InsertOrder(mainOrder, connection, transaction, cancellationToken);

            if (mainOrder.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is SmsTemplate mainSmsTemplate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InsertSmsTextAsync(mainOrderId, mainSmsTemplate, connection, transaction, cancellationToken);
            }

            if (mainOrder.Templates.Find(e => e.Type == NotificationTemplateType.Email) is EmailTemplate mainEmailTemplate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InsertEmailTextAsync(mainOrderId, mainEmailTemplate, connection, transaction, cancellationToken);
            }

            if (reminders != null)
            {
                foreach (var notificationOrder in reminders)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    long reminderOrderId = await InsertOrder(notificationOrder, connection, transaction, cancellationToken);

                    if (notificationOrder.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is SmsTemplate reminderSmsTemplate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await InsertSmsTextAsync(reminderOrderId, reminderSmsTemplate, connection, transaction, cancellationToken);
                    }

                    if (notificationOrder.Templates.Find(e => e.Type == NotificationTemplateType.Email) is EmailTemplate reminderEmailTemplate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await InsertEmailTextAsync(reminderOrderId, reminderEmailTemplate, connection, transaction, cancellationToken);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return reminders == null ? [mainOrder] : [mainOrder, .. reminders];
    }

    /// <inheritdoc/>
    public async Task SetProcessingStatus(Guid orderId, OrderProcessingStatus status)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setProcessCompleted);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<List<NotificationOrder>> GetPastDueOrdersAndSetProcessingState()
    {
        List<NotificationOrder> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrdersPastSendTimeUpdateStatus);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                NotificationOrder notificationOrder = reader.GetFieldValue<NotificationOrder>(0);
                searchResult.Add(notificationOrder);
            }
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderWithStatus?> GetOrderWithStatusById(Guid id, string creator)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderIncludeStatus);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        NotificationOrderWithStatus? order = null;

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            if (!reader.HasRows)
            {
                return null;
            }

            await reader.ReadAsync();
            order = ReadNotificationOrderWithStatus(reader);
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse?> GetOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using NpgsqlCommand command = _dataSource.CreateCommand(_getOrdersChainTrackingSql);

        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, idempotencyId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        var ordersChainId = await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal("orders_chain_id"), cancellationToken);
        if (ordersChainId == Guid.Empty)
        {
            return null;
        }

        var mainShipmentId = await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal("shipment_id"), cancellationToken);
        if (mainShipmentId == Guid.Empty)
        {
            return null;
        }

        string? mainSendersReference = await reader.IsDBNullAsync(reader.GetOrdinal("senders_reference"), cancellationToken) ?
            null :
            reader.GetString(reader.GetOrdinal("senders_reference"));

        var reminderShipments = await ExtractReminderShipmentsTracking(reader, cancellationToken);

        return CreateNotificationOrderChainResponse(ordersChainId, mainShipmentId, mainSendersReference, reminderShipments, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrderWithStatus, CancellationError>> CancelOrder(Guid id, string creator)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_cancelAndReturnOrder);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return CancellationError.OrderNotFound;
        }

        await reader.ReadAsync();
        bool canCancel = reader.GetValue<bool>("cancelallowed");

        if (!canCancel)
        {
            return CancellationError.CancellationProhibited;
        }

        NotificationOrderWithStatus? order = ReadNotificationOrderWithStatus(reader);
        return order!;
    }

    private static NotificationOrderWithStatus? ReadNotificationOrderWithStatus(NpgsqlDataReader reader)
    {
        string? conditionEndpointString = reader.GetValue<string>("conditionendpoint");
        Uri? conditionEndpoint = conditionEndpointString == null ? null : new Uri(conditionEndpointString);

        NotificationOrderWithStatus order = new(
             reader.GetValue<Guid>("alternateid"),
             reader.GetValue<string>("sendersreference"),
             reader.GetValue<DateTime>("requestedsendtime"), // all decimals are not included
             new Creator(reader.GetValue<string>("creatorname")),
             reader.GetValue<DateTime>("created"),
             reader.GetValue<NotificationChannel>("notificationchannel"),
             reader.GetValue<bool?>("ignorereservation"),
             reader.GetValue<string?>("resourceid"),
             conditionEndpoint,
             new ProcessingStatus(reader.GetValue<OrderProcessingStatus>("processedstatus"), reader.GetValue<DateTime>("processed")),
             OrderTypes.Notification);

        int generatedEmail = (int)reader.GetValue<long>("generatedEmailCount");
        int succeededEmail = (int)reader.GetValue<long>("succeededEmailCount");

        int generatedSms = (int)reader.GetValue<long>("generatedSmsCount");
        int succeededSms = (int)reader.GetValue<long>("succeededSmsCount");

        if (generatedEmail > 0)
        {
            order.SetNotificationStatuses(NotificationTemplateType.Email, generatedEmail, succeededEmail);
        }

        if (generatedSms > 0)
        {
            order.SetNotificationStatuses(NotificationTemplateType.Sms, generatedSms, succeededSms);
        }

        return order;
    }

    /// <summary>
    /// Extracts reminder shipment information from the JSON array returned by the database query.
    /// </summary>
    /// <param name="reader">The database reader containing the result from the notification order chain tracking query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A list of <see cref="NotificationOrderChainShipment"/> objects representing all reminder shipments
    /// associated with the notification order chain, or an empty list if no reminders exist.
    /// </returns>
    /// <remarks>
    /// This method processes the "reminders" JSON column from the database result set, which contains
    /// an array of objects with "ShipmentId" and "SendersReference" properties. The ShipmentId uniquely
    /// identifies each reminder notification order, while the SendersReference is an optional
    /// field that clients can use to correlate shipments with their systems.
    /// </remarks>
    private static async Task<List<NotificationOrderChainShipment>> ExtractReminderShipmentsTracking(NpgsqlDataReader reader, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int remindersOrdinal = reader.GetOrdinal("reminders");
        if (await reader.IsDBNullAsync(remindersOrdinal, cancellationToken))
        {
            return [];
        }

        var remindersJson = await reader.GetFieldValueAsync<JsonElement>(remindersOrdinal, cancellationToken);
        if (remindersJson.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var reminderShipments = new List<NotificationOrderChainShipment>();

        foreach (var reminder in remindersJson.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var shipmentId = Guid.Empty;
            if (reminder.TryGetProperty("ShipmentId", out var shipmentIdElement) && shipmentIdElement.ValueKind != JsonValueKind.Null)
            {
                shipmentId = shipmentIdElement.GetGuid();
            }

            string? sendersReference = null;
            if (reminder.TryGetProperty("SendersReference", out var sendersReferenceElement) && sendersReferenceElement.ValueKind != JsonValueKind.Null)
            {
                sendersReference = sendersReferenceElement.GetString();
            }

            reminderShipments.Add(new NotificationOrderChainShipment
            {
                ShipmentId = shipmentId,
                SendersReference = sendersReference
            });
        }

        return reminderShipments;
    }

    private static async Task<long> InsertOrder(NotificationOrder order, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = new(_insertOrderSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, order.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.SendersReference ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.Created);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.RequestedSendTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, order);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, (int?)order.SendingTimePolicy ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Type.ToString());

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        long orderId = (long)reader.GetValue(0);
        return orderId;
    }

    private static async Task InsertSmsTextAsync(long dbOrderId, SmsTemplate? smsTemplate, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (smsTemplate == null)
        {
            return;
        }

        await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertSmsTextSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.SenderNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.Body);

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEmailTextAsync(long dbOrderId, EmailTemplate? emailTemplate, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (emailTemplate == null)
        {
            return;
        }

        await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertEmailTextSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.FromAddress);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.Subject);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.Body);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.ContentType.ToString());

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOrderChainAsync(NotificationOrderChainRequest orderChain, DateTime creationDateTime, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = new(_insertorderchainSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderChain.OrderChainId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, orderChain.IdempotencyId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, orderChain.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, creationDateTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, orderChain);

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a notification order chain response object.
    /// </summary>
    /// <param name="orderChainId">The unique identifier for the main notification order chain.</param>
    /// <param name="shipmentId">The unique identifier for the main notification order shipment.</param>
    /// <param name="sendersReference">The sender's reference for the main notification order shipment.</param>
    /// <param name="reminders">The list of reminder shipments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A fully constructed notification order chain response.</returns>
    private static NotificationOrderChainResponse CreateNotificationOrderChainResponse(Guid orderChainId, Guid shipmentId, string? sendersReference, List<NotificationOrderChainShipment> reminders, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new NotificationOrderChainResponse
        {
            OrderChainId = orderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = shipmentId,
                SendersReference = sendersReference,
                Reminders = reminders.Count > 0 ? reminders : null
            }
        };
    }
}
