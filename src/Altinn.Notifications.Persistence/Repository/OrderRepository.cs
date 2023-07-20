using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private const string _insertOrderSql = "select notifications.insertorder($1, $2, $3, $4, $5, $6)"; // (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _notificationorder)
    private const string _insertEmailTextSql = "call notifications.insertemailtext($1, $2, $3, $4, $5)"; // (__orderid, _fromaddress, _subject, _body, _contenttype)
    private const string _getOrderById = "select notificationorder from notifications.orders where alternateid = $1";
    private const string _setProcessCompleted = "update notifications.orders set processedstatus = 'completed' where alternateid = $1";
    private const string _getOrdersPastSendTimeUpdateStatus = "select notifications.getorders_pastsendtime_updatestatus()";

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public OrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder> Create(NotificationOrder order)
    {
        long dbOrderId = await InsertOrder(order);

        EmailTemplate? emailTemplate = ExtractTemplates(order);
        if (emailTemplate != null)
        {
            await InsertEmailText(dbOrderId, emailTemplate.FromAddress, emailTemplate.Subject, emailTemplate.Body, emailTemplate.ContentType.ToString());
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrder?> GetById(string id)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderById);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(id));

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();
        var serialized = reader.GetString(0);

        return NotificationOrder.Deserialize(serialized);
    }

    /// <inheritdoc/>
    public async Task SetProcessingCompleted(string orderId)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setProcessCompleted);

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
                NotificationOrder notificationOrder = NotificationOrder.Deserialize(reader[0]?.ToString()!)!;
                searchResult.Add(notificationOrder);
            }
        }

        return searchResult;
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

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(order.Id));
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.Creator.ShortName);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, order.SendersReference ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.Created);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, order.RequestedSendTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, order.Serialize());

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (long)reader.GetValue(0);
    }

    private async Task InsertEmailText(long dbOrderId, string fromAddress, string subject, string body, string contentType)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailTextSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, dbOrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, fromAddress);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, subject);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, body);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, contentType);

        await pgcom.ExecuteNonQueryAsync();
    }
}