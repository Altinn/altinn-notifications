using System.Data;

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
public class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _getOrderByIdSql = "select notificationorder from notifications.orders where alternateid=$1 and creatorname=$2";
    private const string _getOrdersBySendersReferenceSql = "select notificationorder from notifications.orders where sendersreference=$1 and creatorname=$2";
    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder)
    private const string _insertEmailTextSql = "call notifications.insertemailtext($1, $2, $3, $4, $5)"; // (__orderid, _fromaddress, _subject, _body, _contenttype)
    private const string _insertSmsTextSql = "insert into notifications.smstexts(_orderid, sendernumber, body) VALUES ($1, $2, $3)"; // __orderid, _sendernumber, _body
    private const string _setProcessCompleted = "update notifications.orders set processedstatus =$1::orderprocessingstate where alternateid=$2";
    private const string _getOrdersPastSendTimeUpdateStatus = "select notifications.getorders_pastsendtime_updatestatus()";
    private const string _getOrderIncludeStatus = "select * from notifications.getorder_includestatus_v4($1, $2)"; // _alternateid,  creator
    private const string _cancelAndReturnOrder = "select * from notifications.cancelorder($1, $2)"; // _alternateid,  creator
    private const string _insertorderchainSql = "call notifications.insertorderchain($1, $2, $3, $4, $5)"; // (_orderid, _idempotencyid, _creatorname, _created, _orderchain)

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
    public async Task<List<NotificationOrder>> Create(NotificationOrderChainRequest orderRequest, NotificationOrder mainNotificationOrder, List<NotificationOrder> reminders)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await InsertOrderChain(orderRequest, mainNotificationOrder.Created, connection, transaction);

            long mainOrderId = await InsertOrder(mainNotificationOrder, connection, transaction);

            EmailTemplate? mainEmailTemplate = mainNotificationOrder.Templates.Find(t => t.Type == NotificationTemplateType.Email) as EmailTemplate;
            await InsertEmailTextAsync(mainOrderId, mainEmailTemplate, connection, transaction);

            SmsTemplate? mainSmsTemplate = mainNotificationOrder.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
            await InsertSmsTextAsync(mainOrderId, mainSmsTemplate, connection, transaction);

            if (reminders != null)
            {
                foreach (var notificationOrder in reminders)
                {
                    long reminderOrderId = await InsertOrder(notificationOrder, connection, transaction);

                    EmailTemplate? reminderEmailTemplate = notificationOrder.Templates.Find(t => t.Type == NotificationTemplateType.Email) as EmailTemplate;
                    await InsertEmailTextAsync(reminderOrderId, reminderEmailTemplate, connection, transaction);

                    SmsTemplate? reminderSmsTemplate = notificationOrder.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
                    await InsertSmsTextAsync(reminderOrderId, reminderSmsTemplate, connection, transaction);
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }

        return reminders == null ? [mainNotificationOrder] : [mainNotificationOrder, .. reminders];
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
             new ProcessingStatus(
                 reader.GetValue<OrderProcessingStatus>("processedstatus"),
                 reader.GetValue<DateTime>("processed")));

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

    private static async Task<long> InsertOrder(NotificationOrder order, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertOrderSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, order.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.SendersReference ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.Created);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.RequestedSendTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, order);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        long orderId = (long)reader.GetValue(0);
        return orderId;
    }

    private static async Task InsertSmsTextAsync(long dbOrderId, SmsTemplate? smsTemplate, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (smsTemplate != null)
        {
            await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertSmsTextSql, connection, transaction);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.SenderNumber);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.Body);

            await pgcom.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertEmailTextAsync(long dbOrderId, EmailTemplate? emailTemplate, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (emailTemplate != null)
        {
            await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertEmailTextSql, connection, transaction);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.FromAddress);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.Subject);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.Body);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailTemplate.ContentType.ToString());

            await pgcom.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertOrderChain(NotificationOrderChainRequest order, DateTime creationDateTime, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand pgcom = new NpgsqlCommand(_insertorderchainSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, order.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.IdempotencyId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, creationDateTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, order);

        await pgcom.ExecuteNonQueryAsync();
    }
}
