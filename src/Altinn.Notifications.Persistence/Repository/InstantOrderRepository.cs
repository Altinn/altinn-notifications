using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Provides an implementation of <see cref="IInstantOrderRepository"/> for persisting and retrieving instant notification orders using PostgreSQL.
/// </summary>
public class InstantOrderRepository : IInstantOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6, $7, $8, $9)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder, _sendingtimepolicy, _type, _processingstatus)
    private const string _insertSmsTextSql = "insert into notifications.smstexts(_orderid, sendernumber, body) VALUES ($1, $2, $3)"; // __orderid, _sendernumber, _body
    private const string _setProcessCompleted = "update notifications.orders set processedstatus =$1::orderprocessingstate where alternateid=$2";
    private const string _insertorderchainSql = "call notifications.insertorderchain($1, $2, $3, $4, $5)"; // (_orderid, _idempotencyid, _creatorname, _created, _orderchain)
    private const string _getInstantOrderTrackingSql = "SELECT * FROM notifications.get_instant_order_tracking($1, $2)"; // (_creatorname, _idempotencyid)

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrderRepository"/> class.
    /// </summary>
    public InstantOrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrder> Create(InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InsertInstantNotificationOrderAsync(instantNotificationOrder, connection, transaction, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            long mainOrderId = await InsertOrder(notificationOrder, connection, transaction, OrderProcessingStatus.Processed, cancellationToken);

            if (notificationOrder.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is SmsTemplate mainSmsTemplate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InsertSmsTextAsync(mainOrderId, mainSmsTemplate, connection, transaction, cancellationToken);
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

        return instantNotificationOrder;
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

    private async Task SetProcessingStatus(Guid orderId, OrderProcessingStatus status)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setProcessCompleted);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> GetInstantOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        string shipmentIdColumnName = "shipment_id";
        string ordersChainIdColumnName = "orders_chain_id";
        string senderReferenceColumnName = "senders_reference";

        cancellationToken.ThrowIfCancellationRequested();

        await using NpgsqlCommand command = _dataSource.CreateCommand(_getInstantOrderTrackingSql);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, idempotencyId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        var orderChainId = await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal(ordersChainIdColumnName), cancellationToken);
        if (orderChainId == Guid.Empty)
        {
            return null;
        }

        var shipmentId = await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal(shipmentIdColumnName), cancellationToken);
        if (shipmentId == Guid.Empty)
        {
            return null;
        }

        string? sendersReference = await reader.IsDBNullAsync(reader.GetOrdinal(senderReferenceColumnName), cancellationToken)
            ? null
            : reader.GetString(reader.GetOrdinal(senderReferenceColumnName));

        return new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = shipmentId,
                SendersReference = sendersReference
            }
        };
    }

    /// <summary>
    /// Persists an instant notification order to the database as part of a transaction.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The high-priority notification order to be persisted, containing recipient and delivery details.
    /// </param>
    /// <param name="connection">
    /// The active PostgreSQL database connection.
    /// </param>
    /// <param name="transaction">
    /// The transaction context within which the database operation executes.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task InsertInstantNotificationOrderAsync(InstantNotificationOrder instantNotificationOrder, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = new(_insertorderchainSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, instantNotificationOrder.OrderChainId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, instantNotificationOrder.IdempotencyId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, instantNotificationOrder.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, instantNotificationOrder.Created);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, instantNotificationOrder);

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> InsertOrder(NotificationOrder order, NpgsqlConnection connection, NpgsqlTransaction transaction, OrderProcessingStatus processingStatus = default, CancellationToken cancellationToken = default)
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
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, processingStatus.ToString());

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        long orderId = (long)reader.GetValue(0);
        return orderId;
    }
}
