using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class PostgreUtil
{
    // Cached dependencies — resolved once, reused for the entire test run.
    private static readonly Lazy<NpgsqlDataSource> _dataSource = new(() => ServiceUtil.GetSharedDataSource());

    private static readonly Lazy<OrderRepository> _orderRepo = new(() =>
        (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(s => s is OrderRepository));

    private static readonly Lazy<EmailNotificationRepository> _emailNotificationRepo = new(() =>
        (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)]).First(s => s is EmailNotificationRepository));

    private static readonly Lazy<SmsNotificationRepository> _smsNotificationRepo = new(() =>
        (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)]).First(s => s is SmsNotificationRepository));

    private static NpgsqlDataSource DataSource => _dataSource.Value;

    private static OrderRepository OrderRepo => _orderRepo.Value;

    private static EmailNotificationRepository EmailNotificationRepo => _emailNotificationRepo.Value;

    private static SmsNotificationRepository SmsNotificationRepo => _smsNotificationRepo.Value;

    public static async Task<Guid> PopulateDBWithEmailOrderAndReturnId(string? sendersReference = null)
    {
        var order = await PopulateDBWithEmailOrder(sendersReference);
        return order.Id;
    }

    public static async Task<Guid> PopulateDBWithSmsOrderAndReturnId(string? sendersReference = null)
    {
        var order = await PopulateDBWithSmsOrder(sendersReference);
        return order.Id;
    }

    public static async Task<NotificationOrder> PopulateDBWithEmailOrder(string? sendersReference = null)
    {
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        var persistedOrder = await OrderRepo.Create(order);
        return persistedOrder;
    }

    public static async Task DeleteOrdersByRefPrefix(string sendersRefPrefix)
    {
        string sql = @"
        DELETE FROM notifications.statusfeed 
        WHERE orderid IN (
            SELECT _id FROM notifications.orders WHERE sendersreference LIKE @prefix
        );
        DELETE FROM notifications.emailnotifications 
        WHERE _orderid IN (
            SELECT _id FROM notifications.orders WHERE sendersreference LIKE @prefix
        );
        DELETE FROM notifications.smsnotifications 
        WHERE _orderid IN (
            SELECT _id FROM notifications.orders WHERE sendersreference LIKE @prefix
        );
        DELETE FROM notifications.orders WHERE sendersreference LIKE @prefix;";

        await RunSql(sql, new NpgsqlParameter("@prefix", $"{sendersRefPrefix}%"));
    }

    public static async Task<NotificationOrder> PopulateDBWithSmsOrder(string? sendersReference = null)
    {
        var order = TestdataUtil.NotificationOrder_SmsTemplate_OneRecipient();
        order.Id = Guid.NewGuid();

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        var persistedOrder = await OrderRepo.Create(order);
        return persistedOrder;
    }

    public static async Task<(NotificationOrder Order, EmailNotification EmailNotification)> PopulateDBWithOrderAndEmailNotification(string toAddress)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();
        e.Recipient.ToAddress = toAddress;

        await OrderRepo.Create(o);
        await OrderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processing);
        await EmailNotificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
        await OrderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processed);
        return (o, e);
    }

    public static async Task<(NotificationOrder Order, EmailNotification EmailNotification)> PopulateDBWithOrderAndEmailNotification(string? sendersReference = null, bool simulateCronJob = false, bool simulateConsumers = false, bool forceSendersReferenceToBeNull = false)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();

        if (sendersReference != null)
        {
            o.SendersReference = sendersReference;
        }

        if (forceSendersReferenceToBeNull)
        {             
            // Force the senders reference to be null, even if a value is provided
            o.SendersReference = null;
        }

        /*
         * Notes:
         * 1. When a new notification order is created in the database, its processing status is 'Registered'.
         * 2. When handling of a registered order begins, its processing status should be updated to 'Processing'.
         * 3. Once handling of a notification order in the 'Processing' state is done, its processing status should be updated to 'Processed'.
         */
        if (simulateCronJob && simulateConsumers)
        {
            await OrderRepo.Create(o);
            await OrderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processing);
            await EmailNotificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
            await OrderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processed);
        }
        else if (simulateCronJob && !simulateConsumers)
        {
            await OrderRepo.Create(o);
            await OrderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processing);
            await EmailNotificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
        }
        else
        {
            await OrderRepo.Create(o);
            await EmailNotificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
        }

        return (o, e);
    }

    public static async Task<NotificationOrder> PopulateDBWithOrderAndEmailNotificationReturnOrder(string? sendersReference = null)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();

        if (sendersReference != null)
        {
            o.SendersReference = sendersReference;
        }

        await OrderRepo.Create(o);
        await EmailNotificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));

        return o;
    }

    public static async Task<(NotificationOrder Order, SmsNotification SmsNotification)> PopulateDBWithOrderAndSmsNotification(string? sendersReference = null, SendingTimePolicy? sendingTimePolicy = null, bool simulateCronJob = false, bool simulateConsumers = false, bool forceSendersReferenceToBeNull = false, SmsNotificationResultType? resultType = null)
    {
        (NotificationOrder order, SmsNotification smsNotification) = TestdataUtil.GetOrderAndSmsNotification(sendingTimePolicy);

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        if (forceSendersReferenceToBeNull)
        {
            // Force the senders reference to be null, even if a value is provided
            order.SendersReference = null;
        }

        if (resultType.HasValue)
        {
            smsNotification.SendResult = new NotificationResult<SmsNotificationResultType>(resultType.Value, DateTime.UtcNow.AddDays(-1));
        }

        /*
        * Notes:
        * 1. When a new notification order is created in the database, its processing status is 'Registered'.
        * 2. When handling of a registered order begins, its processing status should be updated to 'Processing'.
        * 3. Once handling of a notification order in the 'Processing' state is done, its processing status should be updated to 'Processed'.
        */
        if (simulateCronJob && simulateConsumers)
        {
            await OrderRepo.Create(order);
            await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
            await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);
        }
        else if (simulateCronJob && !simulateConsumers)
        {
            await OrderRepo.Create(order);
            await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
        }
        else
        {
            await OrderRepo.Create(order);
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
        }

        return (order, smsNotification);
    }

    public static async Task<NotificationOrder> PopulateDBWithOrderAnd4Notifications(string orgName, DateTime? timestamp = null)
    {
        // Get test data for base order with one notification
        (NotificationOrder order, SmsNotification smsNotificationFirst) = TestdataUtil.GetOrderAndSmsNotification();
        order.Creator = new Core.Models.Creator(orgName);
        var timeStamp = timestamp ?? DateTime.UtcNow;
        order.RequestedSendTime = timeStamp;
        smsNotificationFirst.RequestedSendTime = timeStamp;
        smsNotificationFirst.SendResult = new(SmsNotificationResultType.Sending, timeStamp);
        smsNotificationFirst.OrderId = order.Id;

        var smsNotificationSecond = new SmsNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                MobileNumber = smsNotificationFirst.Recipient.MobileNumber,
            },
            SendResult = new(SmsNotificationResultType.Sending, timeStamp)
        };

        var emailNotificationFirst = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                ToAddress = "noreply@altinn.no"
            },
            SendResult = new(EmailNotificationResultType.Sending, timeStamp)
        };

        var emailNotificationSecond = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                ToAddress = "noreply@altinn.no"
            },
            SendResult = new(EmailNotificationResultType.Sending, timeStamp),
        };

        await OrderRepo.Create(order);
        await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        await SmsNotificationRepo.AddNotification(smsNotificationFirst, DateTime.UtcNow.AddDays(1), 1);
        await SmsNotificationRepo.AddNotification(smsNotificationSecond, DateTime.UtcNow.AddDays(1), 1);
        await EmailNotificationRepo.AddNotification(emailNotificationFirst, DateTime.UtcNow.AddDays(1));
        await EmailNotificationRepo.AddNotification(emailNotificationSecond, DateTime.UtcNow.AddDays(1));

        await EmailNotificationRepo.UpdateSendStatus(emailNotificationFirst.Id, EmailNotificationResultType.Delivered);
        await EmailNotificationRepo.UpdateSendStatus(emailNotificationSecond.Id, EmailNotificationResultType.Delivered);

        await SmsNotificationRepo.UpdateSendStatus(smsNotificationFirst.Id, SmsNotificationResultType.Delivered);
        await SmsNotificationRepo.UpdateSendStatus(smsNotificationSecond.Id, SmsNotificationResultType.Delivered);

        await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);

        return order;
    }

    public static async Task DeleteOrderFromDb(string sendersRef)
    {
        string sql = "DELETE FROM notifications.orders WHERE sendersreference = @sendersRef";

        await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("@sendersRef", sendersRef);

        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task DeleteOrderFromDb(Guid id)
    {
        string sql = "DELETE FROM notifications.orders WHERE alternateid = @id";

        await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("id", id);

        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task<int> SelectStatusFeedEntryCount(Guid id)
    {
        var sql = @$"SELECT COUNT(*) FROM notifications.statusfeed s
                     INNER JOIN notifications.orders o ON o._id = s.orderid
                     WHERE o.alternateid = '{id}'";
        var result = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        return result;
    }

    public static async Task DeleteStatusFeedFromDb(string sendersRef)
    {
        string sql = @"DELETE FROM notifications.statusfeed s
                       USING notifications.orders o
                       WHERE s.orderid = o._id AND o.sendersreference = @sendersRef;";
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("sendersRef", sendersRef);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task DeleteStatusFeedFromDb(Guid id)
    {
        string sql = @"DELETE FROM notifications.statusfeed s
                       USING notifications.orders o
                       WHERE s.orderid = o._id AND o.alternateid = @id";
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("id", id);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task UpdateResultAndExpiryTimeNotification<T>(T notification, string timeInterval)
        where T : class
    {
        string sql = string.Empty;
        Guid notificationId;

        if (typeof(T) == typeof(EmailNotification))
        {
            var emailNotification = notification as EmailNotification;
            notificationId = emailNotification!.Id;
            sql = $@"
                UPDATE notifications.emailnotifications 
                SET result = 'Succeeded', 
                    expirytime = NOW() - INTERVAL '{timeInterval}' 
                WHERE alternateid = @id;";
        }
        else if (typeof(T) == typeof(SmsNotification))
        {
            var smsNotification = notification as SmsNotification;
            notificationId = smsNotification!.Id;
            sql = $@"
                UPDATE notifications.smsnotifications 
                SET result = 'Accepted', 
                    expirytime = NOW() - INTERVAL '{timeInterval}' 
                WHERE alternateid = @id;";
        }
        else
        {
            throw new ArgumentException("Type T must be either EmailNotification or SmsNotification");
        }

        await RunSql(
            sql,
            new NpgsqlParameter("@id", notificationId));
    }

    public static async Task<T> RunSqlReturnOutput<T>(string query)
    {
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(query);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        T result = reader.GetValue<T>(0);
        return result;
    }

    public static async Task<string?> GetStatusFeedOrderStatusJson(Guid orderId)
    {
        var sql = @"SELECT s.orderstatus::text 
                    FROM notifications.statusfeed s
                    INNER JOIN notifications.orders o ON o._id = s.orderid
                    WHERE o.alternateid = @orderId
                    LIMIT 1";

        await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("orderId", orderId);

        var result = await pgcom.ExecuteScalarAsync();
        return result?.ToString();
    }

    public static async Task RunSql(string query)
    {
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(query);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task RunSql(string query, params NpgsqlParameter[] parameters)
    {
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(query);
        
        if (parameters.Length > 0)
        {
            pgcom.Parameters.AddRange(parameters);
        }
        
        await pgcom.ExecuteNonQueryAsync();
    }

    public static Task<long?> GetDeadDeliveryReportIdFromOperationId(string operationId)
        => GetDeadDeliveryReportIdByJsonField("operationId", operationId);

    public static Task<long?> GetDeadDeliveryReportIdFromGatewayReference(string gatewayReference)
        => GetDeadDeliveryReportIdByJsonField("gatewayReference", gatewayReference);
    
    public static async Task UpdateNotificationCustomizedContent<T>(Guid notificationId, string? customizedSubject, string customizedBody)
        where T : class
    {
        string updateSql;

        if (typeof(T) == typeof(EmailNotification))
        {
            updateSql = @"
                    UPDATE notifications.emailnotifications 
                    SET customizedsubject = @customizedSubject, 
                        customizedbody = @customizedBody
                    WHERE alternateid = @notificationId";

            await RunSql(
                updateSql,
                new NpgsqlParameter("@notificationId", notificationId),
                new NpgsqlParameter("@customizedSubject", customizedSubject ?? (object)DBNull.Value),
                new NpgsqlParameter("@customizedBody", customizedBody));
        }
        else if (typeof(T) == typeof(SmsNotification))
        {
            updateSql = @"
                    UPDATE notifications.smsnotifications 
                    SET customizedbody = @customizedBody
                    WHERE alternateid = @notificationId";

            await RunSql(
                updateSql,
                new NpgsqlParameter("@notificationId", notificationId),
                new NpgsqlParameter("@customizedBody", customizedBody));
        }
        else
        {
            throw new ArgumentException("Type T must be either EmailNotification or SmsNotification");
        }
    }

    public static async Task UpdateNotificationResult<T>(Guid orderId, string result)
        where T : class
    {
        if (typeof(T) == typeof(SmsNotification))
        {
            string sql = @"
                UPDATE notifications.smsnotifications 
                SET result = @result::smsnotificationresulttype 
                WHERE _orderid = (SELECT _id FROM notifications.orders WHERE alternateid = @orderId)";
            await RunSql(
                sql,
                new NpgsqlParameter("@result", result),
                new NpgsqlParameter("@orderId", orderId));
        }
        else if (typeof(T) == typeof(EmailNotification))
        {
            string sql = @"
                UPDATE notifications.emailnotifications 
                SET result = @result::emailnotificationresulttype 
                WHERE _orderid = (SELECT _id FROM notifications.orders WHERE alternateid = @orderId)";
            await RunSql(
                sql,
                new NpgsqlParameter("@result", result),
                new NpgsqlParameter("@orderId", orderId));
        }
        else
        {
            throw new ArgumentException("Type T must be either EmailNotification or SmsNotification");
        }
    }

    private static async Task<long?> GetDeadDeliveryReportIdByJsonField(string fieldName, string fieldValue)
    {
        // Validate fieldName to prevent SQL injection
        var allowedFields = new HashSet<string> { "operationId", "gatewayReference" };
        if (!allowedFields.Contains(fieldName))
        {
            throw new ArgumentException($"Invalid field name: {fieldName}. Allowed fields: {string.Join(", ", allowedFields)}", nameof(fieldName));
        }

        var query = $@"SELECT id FROM notifications.deaddeliveryreports WHERE deliveryreport ->> '{fieldName}' = @fieldValue";

        await using NpgsqlCommand pgcom = DataSource.CreateCommand(query);
        pgcom.Parameters.AddWithValue("@fieldValue", fieldValue);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return await reader.GetFieldValueAsync<long>(0);
    }

    /// <summary>
    /// Deletes orders from the database by their alternate IDs.
    /// </summary>
    /// <param name="orderIds">Collection of order alternate IDs to delete.</param>
    public static async Task DeleteOrdersByAlternateIds(IEnumerable<Guid> orderIds)
    {
        if (orderIds is null || !orderIds.Any())
        {
            return;
        }

        string deleteSql = @"DELETE from notifications.orders o where o.alternateid = ANY(@orderIds)";
        await RunSql(deleteSql, new NpgsqlParameter("orderIds", orderIds.ToArray()));
    }
}
