using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Provides an implementation of <see cref="IInstantOrderRepository"/> for persisting and retrieving instant notification orders.
/// </summary>
public class InstantOrderRepository : IInstantOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6, $7, $8, $9)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder, _sendingtimepolicy, _type, _processingstatus)
    private const string _insertSmsTextSql = "insert into notifications.smstexts(_orderid, sendernumber, body) VALUES ($1, $2, $3)"; // (_orderid, _sendernumber, _body)
    private const string _insertorderchainSql = "call notifications.insertorderchain($1, $2, $3, $4, $5)"; // (_orderid, _idempotencyid, _creatorname, _created, _orderchain)
    private const string _getInstantOrderTrackingSql = "SELECT * FROM notifications.get_instant_order_tracking($1, $2)"; // (_creatorname, _idempotencyid)
    private const string _insertSmsNotificationSql = "call notifications.insertsmsnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _mobilenumber, _customizedbody, _result, _smscount, _resulttime, _expirytime)

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrderRepository"/> class.
    /// </summary>
    public InstantOrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
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

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, SmsNotification smsNotification, DateTime smsExpiryTime, int smsMessageCount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InsertInstantNotificationOrderAsync(instantNotificationOrder, connection, transaction, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            long mainOrderId = await InsertNotificationOrderAsync(notificationOrder, connection, transaction, OrderProcessingStatus.Processed, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var smsTemplate = notificationOrder.Templates.Find(e => e.Type == NotificationTemplateType.Sms) as SmsTemplate;
            await InsertSmsTextAsync(mainOrderId, smsTemplate!, connection, transaction, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await InsertSmsNotificationAsync(smsNotification, smsExpiryTime, smsMessageCount, connection, transaction, cancellationToken);

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

        return new InstantNotificationOrderTracking
        {
            OrderChainId = instantNotificationOrder.OrderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = notificationOrder.Id,
                SendersReference = notificationOrder.SendersReference
            }
        };
    }

    /// <summary>
    /// Persists the SMS template details for a notification order.
    /// </summary>
    /// <param name="notificationOrderId">
    /// The unique identifier of the notification order in the database.
    /// </param>
    /// <param name="smsTemplate">
    /// The <see cref="SmsTemplate"/> containing the sender number and message body to be stored.
    /// </param>
    /// <param name="connection">
    /// The active <see cref="NpgsqlConnection"/> to the PostgreSQL database.
    /// </param>
    /// <param name="transaction">
    /// The <see cref="NpgsqlTransaction"/> context within which the database operation executes.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    private static async Task InsertSmsTextAsync(long notificationOrderId, SmsTemplate smsTemplate, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = new(_insertSmsTextSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, notificationOrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.SenderNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, smsTemplate.Body);

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Persists an instant notification order to the database.
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

    /// <summary>
    /// Persists an SMS notification.
    /// </summary>
    /// <param name="notification">
    /// The <see cref="SmsNotification"/> containing recipient information, message content, and send result details.
    /// </param>
    /// <param name="smsExpiryTime">
    /// The <see cref="DateTime"/> indicating when the SMS notification expires and should no longer be delivered.
    /// </param>
    /// <param name="smsMessageCount">
    /// The number of SMS messages to be sent based on the message content.
    /// </param>
    /// <param name="connection">
    /// The active <see cref="NpgsqlConnection"/> to the PostgreSQL database.
    /// </param>
    /// <param name="transaction">
    /// The <see cref="NpgsqlTransaction"/> context within which the database operation executes.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    private static async Task InsertSmsNotificationAsync(SmsNotification notification, DateTime smsExpiryTime, int smsMessageCount, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using NpgsqlCommand pgcom = new(_insertSmsNotificationSql, connection, transaction);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.MobileNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, smsMessageCount);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, smsExpiryTime);

        await pgcom.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Persists a notification order to the database and returns the generated order identifier.
    /// </summary>
    /// <param name="order">
    /// The <see cref="NotificationOrder"/> containing the details of the notification order to be persisted.
    /// </param>
    /// <param name="connection">
    /// The active <see cref="NpgsqlConnection"/> to the PostgreSQL database.
    /// </param>
    /// <param name="transaction">
    /// The <see cref="NpgsqlTransaction"/> context within which the database operation executes.
    /// </param>
    /// <param name="processingStatus">
    /// The <see cref="OrderProcessingStatus"/> indicating the current processing state of the order.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing the generated order ID as a <see cref="long"/>.
    /// </returns>
    private static async Task<long> InsertNotificationOrderAsync(NotificationOrder order, NpgsqlConnection connection, NpgsqlTransaction transaction, OrderProcessingStatus processingStatus = default, CancellationToken cancellationToken = default)
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

        return (long)reader.GetValue(0);
    }
}
