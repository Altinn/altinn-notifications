using System.Data;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Extensions;
using Microsoft.ApplicationInsights;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TelemetryClient _telemetryClient;

    private const string _getOrderByIdSql = "select notificationorder from notifications.orders where alternateid=$1 and creatorname=$2";
    private const string _getOrdersBySendersReferenceSql = "select notificationorder from notifications.orders where sendersreference=$1 and creatorname=$2";
    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder)
    private const string _insertEmailTextSql = "call notifications.insertemailtext($1, $2, $3, $4, $5)"; // (__orderid, _fromaddress, _subject, _body, _contenttype)
    private const string _setProcessCompleted = "update notifications.orders set processedstatus =$1::orderprocessingstate where alternateid=$2";
    private const string _getOrdersPastSendTimeUpdateStatus = "select notifications.getorders_pastsendtime_updatestatus()";
    private const string _getOrderIncludeStatus = "select * from notifications.getorder_includestatus($1, $2)"; // _alternateid,  creator

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="telemetryClient">Telemetry client</param>
    public OrderRepository(NpgsqlDataSource dataSource, TelemetryClient telemetryClient = null)
    {
        _dataSource = dataSource;
        _telemetryClient = telemetryClient;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder?> GetOrderById(Guid id, string creator)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderByIdSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
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

        tracker.Track();
        return order;
    }

    /// <inheritdoc/>
    public async Task<List<NotificationOrder>> GetOrdersBySendersReference(string sendersReference, string creator)
    {
        List<NotificationOrder> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrdersBySendersReferenceSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
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

        tracker.Track();
        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder> Create(NotificationOrder order)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            long dbOrderId = await InsertOrder(order);

            EmailTemplate? emailTemplate = ExtractTemplates(order);
            if (emailTemplate != null)
            {
                await InsertEmailText(dbOrderId, emailTemplate.FromAddress, emailTemplate.Subject, emailTemplate.Body, emailTemplate.ContentType.ToString());
            }

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
    public async Task SetProcessingStatus(Guid orderId, OrderProcessingStatus status)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setProcessCompleted);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await pgcom.ExecuteNonQueryAsync();
        tracker.Track();
    }

    /// <inheritdoc/>
    public async Task<List<NotificationOrder>> GetPastDueOrdersAndSetProcessingState()
    {
        List<NotificationOrder> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrdersPastSendTimeUpdateStatus);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                NotificationOrder notificationOrder = reader.GetFieldValue<NotificationOrder>(0);
                searchResult.Add(notificationOrder);
            }
        }

        tracker.Track();
        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderWithStatus?> GetOrderWithStatusById(Guid id, string creator)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderIncludeStatus);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        NotificationOrderWithStatus? order = null;

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                order = new(
                    reader.GetValue<Guid>("alternateid"),
                    reader.GetValue<string>("sendersreference"),
                    reader.GetValue<DateTime>("requestedsendtime"), // all decimals are not included
                    new Creator(reader.GetValue<string>("creatorname")),
                    reader.GetValue<DateTime>("created"),
                    NotificationChannel.Email,
                    new ProcessingStatus(
                        reader.GetValue<OrderProcessingStatus>("processedstatus"),
                        reader.GetValue<DateTime>("processed")));

                int generatedEmail = (int)reader.GetValue<long>("generatedEmailCount");
                int succeededEmail = (int)reader.GetValue<long>("succeededEmailCount");

                if (generatedEmail > 0)
                {
                    order.SetNotificationStatuses(NotificationTemplateType.Email, generatedEmail, succeededEmail);
                }
            }
        }

        tracker.Track();
        return order;
    }

    /// <summary>
    /// Extracts relevant templates from a notification order
    /// </summary>
    internal static EmailTemplate? ExtractTemplates(NotificationOrder order)
    {
        EmailTemplate? emailTemplate = null;

        foreach (INotificationTemplate template in order.Templates)
        {
            switch (template.Type)
            {
                case Core.Enums.NotificationTemplateType.Email:
                    emailTemplate = template as EmailTemplate;
                    break;
            }
        }

        return emailTemplate;
    }

    private async Task<long> InsertOrder(NotificationOrder order)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertOrderSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, order.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.SendersReference ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.Created);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.RequestedSendTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, order);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        long orderId = (long)reader.GetValue(0);
        tracker.Track();
        return orderId;
    }

    private async Task InsertEmailText(long dbOrderId, string fromAddress, string subject, string body, string contentType)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailTextSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, fromAddress);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, subject);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, body);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, contentType);

        await pgcom.ExecuteNonQueryAsync();
        tracker.Track();
    }
}
