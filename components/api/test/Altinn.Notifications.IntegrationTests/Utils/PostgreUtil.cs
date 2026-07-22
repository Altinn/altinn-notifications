using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationLog;
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

    private static readonly Lazy<NpgsqlDataSource> _adminDataSource = new(() => ServiceUtil.GetSharedAdminDataSource());

    private static readonly Lazy<OrderRepository> _orderRepo = new(() =>
        (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(s => s is OrderRepository));

    private static readonly Lazy<EmailNotificationRepository> _emailNotificationRepo = new(() =>
        (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)]).First(s => s is EmailNotificationRepository));

    private static readonly Lazy<SmsNotificationRepository> _smsNotificationRepo = new(() =>
        (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)]).First(s => s is SmsNotificationRepository));

    private static NpgsqlDataSource DataSource => _dataSource.Value;

    private static NpgsqlDataSource AdminDataSource => _adminDataSource.Value;

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
        if (string.IsNullOrWhiteSpace(sendersRefPrefix))
        {
            throw new ArgumentException("sendersRefPrefix must be non-empty.", nameof(sendersRefPrefix));
        }

        string sql = "DELETE FROM notifications.orders WHERE sendersreference LIKE @prefix;";

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
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1));
            await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);
        }
        else if (simulateCronJob && !simulateConsumers)
        {
            await OrderRepo.Create(order);
            await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1));
        }
        else
        {
            await OrderRepo.Create(order);
            await SmsNotificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1));
        }

        await RefreshMaterializedViews();
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
        await SmsNotificationRepo.AddNotification(smsNotificationFirst, DateTime.UtcNow.AddDays(1));
        await SmsNotificationRepo.AddNotification(smsNotificationSecond, DateTime.UtcNow.AddDays(1));
        await EmailNotificationRepo.AddNotification(emailNotificationFirst, DateTime.UtcNow.AddDays(1));
        await EmailNotificationRepo.AddNotification(emailNotificationSecond, DateTime.UtcNow.AddDays(1));

        await EmailNotificationRepo.UpdateSendStatus(emailNotificationFirst.Id, EmailNotificationResultType.Delivered);
        await EmailNotificationRepo.UpdateSendStatus(emailNotificationSecond.Id, EmailNotificationResultType.Delivered);

        await SmsNotificationRepo.UpdateSendStatus(smsNotificationFirst.Id, SmsNotificationResultType.Delivered);
        await SmsNotificationRepo.UpdateSendStatus(smsNotificationSecond.Id, SmsNotificationResultType.Delivered);

        await OrderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);

        await RefreshMaterializedViews();
        return order;
    }

    public static async Task RefreshMaterializedViews()
    {
        string sql = @"
            REFRESH MATERIALIZED VIEW notifications.email_metrics_recent;
            REFRESH MATERIALIZED VIEW notifications.sms_metrics_recent;";
        await using NpgsqlCommand pgcom = AdminDataSource.CreateCommand(sql);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task DeleteOrderFromDb(string sendersRef)
    {
        string sql = "DELETE FROM notifications.orders WHERE sendersreference = @sendersRef";

        await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
            pgcom.Parameters.AddWithValue("@sendersRef", sendersRef);
            await pgcom.ExecuteNonQueryAsync();
        });
    }

    public static async Task DeleteOrderFromDb(Guid id)
    {
        string sql = "DELETE FROM notifications.orders WHERE alternateid = @id";

        await ExecuteWithDeadlockRetryAsync(async () =>
        {
            await using NpgsqlCommand pgcom = DataSource.CreateCommand(sql);
            pgcom.Parameters.AddWithValue("id", id);
            await pgcom.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Retries <paramref name="action"/> up to 3 times when a PostgreSQL deadlock (40P01) is detected,
    /// with a short back-off between attempts. PostgreSQL expects callers to retry on deadlock.
    /// </summary>
    private static async Task ExecuteWithDeadlockRetryAsync(Func<Task> action, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "40P01" && attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
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

    /// <summary>
    /// Deletes order chain entries from the database by their order chain IDs.
    /// </summary>
    /// <param name="orderChainIds">Collection of order chain IDs to delete.</param>
    public static async Task DeleteOrdersChainByOrderIds(IEnumerable<Guid> orderChainIds)
    {
        if (orderChainIds is null || !orderChainIds.Any())
        {
            return;
        }

        string deleteSql = @"DELETE FROM notifications.orderschain WHERE orderid = ANY(@orderChainIds)";
        await RunSql(deleteSql, new NpgsqlParameter("orderChainIds", orderChainIds.ToArray()));
    }

    /// <summary>
    /// Deletes notification log entries from the database for the given shipment ID.
    /// </summary>
    public static async Task DeleteNotificationLogFromDb(Guid shipmentId)
    {
        string sql = @"DELETE FROM notifications.notificationlog WHERE shipmentid = @shipmentId";
        await RunSql(sql, new NpgsqlParameter("shipmentId", shipmentId));
    }

    /// <summary>
    /// Returns the number of notification log rows for the given shipment ID.
    /// </summary>
    public static async Task<int> SelectNotificationLogEntryCount(Guid shipmentId)
    {
        var sql = @$"SELECT COUNT(*) FROM notifications.notificationlog WHERE shipmentid = '{shipmentId}'";
        return await RunSqlReturnOutput<int>(sql);
    }

    /// <summary>
    /// Inserts an orderschain row with Dialogporten identifiers and an email order linked to it,
    /// then inserts a delivered email notification. Intended for NotificationLogRepository tests.
    /// </summary>
    /// <returns>The order alternate ID (shipment ID) and the order chain ID.</returns>
    public static async Task<(Guid OrderId, Guid OrderChainId)> PopulateDBWithChainedOrderAndEmailNotification(
        Guid dialogId,
        string transmissionId,
        string toAddress = "log-test@example.com",
        string operationId = "op-id-test-abc123",
        string creatorName = "ttd",
        string resourceId = "ttd-resource")
    {
        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var orderChainJson = $$"""
            {
                "orderId": "{{orderId}}",
                "idempotencyId": "test-{{orderChainId}}",
                "DialogportenAssociation": {
                    "DialogId": "{{dialogId}}",
                    "TransmissionId": "{{transmissionId}}"
                }
            }
            """;

        const string insertChainSql = """
        SELECT notifications.insertorderchain_v2(
            @orderChainId,
            @idempotencyId,
            @creatorName,
            @created,
            @orderChain::jsonb
        )
        """;

        const string insertOrderSql = """
        SELECT notifications.insertorder_v2(
            @alternateid,
            @creatorname,
            @sendersreference,
            @created,
            @requestedsendtime,
            @notificationorder::jsonb,
            @sendingtimepolicy,
            @type,
            @processingstatus,
            @orderchainid
        )
        """;

        const string insertEmailSql = """
        CALL notifications.insertemailnotification_v2(
            @orderid,
            @alternateid,
            @recipientorgno,
            @recipientnin,
            @toaddress,
            @customizedbody,
            @customizedsubject,
            @result,
            @resulttime,
            @expirytime,
            @totalattachmentsizebytes
        )
        """;

        const string updateEmailSql = """
        SELECT notifications.updateemailnotification_v4(
            @result,
            @operationid,
            @alternateid,
            NULL::jsonb,
            @totalattachmentsizebytes
        )
        """;

        var notificationOrderJson = $$"""
            {
                "Id": "{{orderId}}",
                "ResourceId": "{{resourceId}}",
                "Creator": {"ShortName": "{{creatorName}}"},
                "Created": "{{now:O}}",
                "RequestedSendTime": "{{now:O}}",
                "NotificationChannel": "email",
                "Type": 0,
                "Templates": [],
                "Recipients": []
            }
            """;

        long chainDbId;
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (NpgsqlCommand cmd = new(insertChainSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@orderChainId", orderChainId);
                cmd.Parameters.AddWithValue("@idempotencyId", $"test-{orderChainId}");
                cmd.Parameters.AddWithValue("@creatorName", creatorName);
                cmd.Parameters.AddWithValue("@created", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@orderChain", orderChainJson);
                chainDbId = (long)(await cmd.ExecuteScalarAsync())!;
            }

            await using (NpgsqlCommand cmd = new(insertOrderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, orderId);
                cmd.Parameters.AddWithValue("@creatorname", NpgsqlTypes.NpgsqlDbType.Text, creatorName);
                cmd.Parameters.AddWithValue("@sendersreference", DBNull.Value);
                cmd.Parameters.AddWithValue("@created", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@requestedsendtime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@notificationorder", notificationOrderJson);
                cmd.Parameters.AddWithValue("@sendingtimepolicy", DBNull.Value);
                cmd.Parameters.AddWithValue("@type", NpgsqlTypes.NpgsqlDbType.Text, "Notification");
                cmd.Parameters.AddWithValue("@processingstatus", NpgsqlTypes.NpgsqlDbType.Text, "Processing");
                cmd.Parameters.AddWithValue("@orderchainid", NpgsqlTypes.NpgsqlDbType.Bigint, chainDbId);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new(insertEmailSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@orderid", NpgsqlTypes.NpgsqlDbType.Uuid, orderId);
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, notificationId);
                cmd.Parameters.AddWithValue("@recipientorgno", DBNull.Value);
                cmd.Parameters.AddWithValue("@recipientnin", DBNull.Value);
                cmd.Parameters.AddWithValue("@toaddress", NpgsqlTypes.NpgsqlDbType.Text, toAddress);
                cmd.Parameters.AddWithValue("@customizedbody", DBNull.Value);
                cmd.Parameters.AddWithValue("@customizedsubject", DBNull.Value);
                cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Text, "New");
                cmd.Parameters.AddWithValue("@resulttime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@expirytime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now.AddDays(1));
                cmd.Parameters.AddWithValue("@totalattachmentsizebytes", NpgsqlTypes.NpgsqlDbType.Bigint, 0L);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new(updateEmailSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Text, "Delivered");
                cmd.Parameters.AddWithValue("@operationid", NpgsqlTypes.NpgsqlDbType.Text, operationId);
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, notificationId);
                cmd.Parameters.AddWithValue("@totalattachmentsizebytes", NpgsqlTypes.NpgsqlDbType.Bigint, 0L);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return (orderId, orderChainId);
    }

    /// <summary>
    /// Inserts an orderschain row with Dialogporten identifiers and an SMS order linked to it,
    /// then inserts a delivered SMS notification. Intended for NotificationLogRepository tests.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description><c>OrderId</c>: The alternate ID of the SMS order (shipment ID).</description></item>
    /// <item><description><c>OrderChainId</c>: The alternate ID (UUID) of the inserted order chain.</description></item>
    /// </list>
    /// </returns>
    public static async Task<(Guid OrderId, Guid OrderChainId)> PopulateDBWithChainedOrderAndSmsNotification(
        Guid dialogId,
        string transmissionId,
        string mobileNumber = "+4799999999",
        string gatewayReference = "gw-ref-test-123",
        string creatorName = "ttd",
        string resourceId = "ttd-resource")
    {
        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var orderChainJson = $$"""
            {
                "orderId": "{{orderId}}",
                "idempotencyId": "test-{{orderChainId}}",
                "DialogportenAssociation": {
                    "DialogId": "{{dialogId}}",
                    "TransmissionId": "{{transmissionId}}"
                }
            }
            """;

        const string insertChainSql = """
        SELECT notifications.insertorderchain_v2(
            @orderChainId,
            @idempotencyId,
            @creatorName,
            @created,
            @orderChain::jsonb
        )
        """;

        const string insertOrderSql = """
        SELECT notifications.insertorder_v2(
            @alternateid,
            @creatorname,
            @sendersreference,
            @created,
            @requestedsendtime,
            @notificationorder::jsonb,
            @sendingtimepolicy,
            @type,
            @processingstatus,
            @orderchainid
        )
        """;

        const string insertSmsSql = """
        CALL notifications.insertsmsnotification_v2(
            @orderid,
            @alternateid,
            @recipientorgno,
            @recipientnin,
            @mobilenumber,
            @customizedbody,
            @result,
            @resulttime,
            @expirytime
        )
        """;

        const string updateSmsSql = """
        SELECT notifications.updatesmsnotification_v3(
            @result,
            @gatewayreference,
            @alternateid,
            NULL::jsonb
        )
        """;

        var notificationOrderJson = $$"""
            {
                "Id": "{{orderId}}",
                "ResourceId": "{{resourceId}}",
                "Creator": {"ShortName": "{{creatorName}}"},
                "Created": "{{now:O}}",
                "RequestedSendTime": "{{now:O}}",
                "NotificationChannel": "sms",
                "Type": 0,
                "Templates": [],
                "Recipients": []
            }
            """;

        long chainDbId;
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (NpgsqlCommand cmd = new(insertChainSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@orderChainId", orderChainId);
                cmd.Parameters.AddWithValue("@idempotencyId", $"test-{orderChainId}");
                cmd.Parameters.AddWithValue("@creatorName", creatorName);
                cmd.Parameters.AddWithValue("@created", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@orderChain", orderChainJson);
                chainDbId = (long)(await cmd.ExecuteScalarAsync())!;
            }

            await using (NpgsqlCommand cmd = new(insertOrderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, orderId);
                cmd.Parameters.AddWithValue("@creatorname", NpgsqlTypes.NpgsqlDbType.Text, creatorName);
                cmd.Parameters.AddWithValue("@sendersreference", DBNull.Value);
                cmd.Parameters.AddWithValue("@created", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@requestedsendtime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@notificationorder", notificationOrderJson);
                cmd.Parameters.AddWithValue("@sendingtimepolicy", DBNull.Value);
                cmd.Parameters.AddWithValue("@type", NpgsqlTypes.NpgsqlDbType.Text, "Notification");
                cmd.Parameters.AddWithValue("@processingstatus", NpgsqlTypes.NpgsqlDbType.Text, "Processing");
                cmd.Parameters.AddWithValue("@orderchainid", NpgsqlTypes.NpgsqlDbType.Bigint, chainDbId);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new(insertSmsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@orderid", NpgsqlTypes.NpgsqlDbType.Uuid, orderId);
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, notificationId);
                cmd.Parameters.AddWithValue("@recipientorgno", DBNull.Value);
                cmd.Parameters.AddWithValue("@recipientnin", DBNull.Value);
                cmd.Parameters.AddWithValue("@mobilenumber", NpgsqlTypes.NpgsqlDbType.Text, mobileNumber);
                cmd.Parameters.AddWithValue("@customizedbody", DBNull.Value);
                cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Text, "New");
                cmd.Parameters.AddWithValue("@resulttime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now);
                cmd.Parameters.AddWithValue("@expirytime", NpgsqlTypes.NpgsqlDbType.TimestampTz, now.AddDays(1));
                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new(updateSmsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@result", NpgsqlTypes.NpgsqlDbType.Text, "Delivered");
                cmd.Parameters.AddWithValue("@gatewayreference", NpgsqlTypes.NpgsqlDbType.Text, gatewayReference);
                cmd.Parameters.AddWithValue("@alternateid", NpgsqlTypes.NpgsqlDbType.Uuid, notificationId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return (orderId, orderChainId);
    }

    /// <summary>
    /// Reads a single notificationlog entry for the given shipment ID.
    /// Returns null if no row exists.
    /// </summary>
    public static async Task<NotificationLogEntry?> GetNotificationLogEntry(Guid shipmentId)
    {
        const string sql = """
            SELECT
                orderchainid,
                shipmentid,
                notificationid,
                creatorname,
                sendersreference,
                dialogid,
                transmissionid,
                deliveryreference,
                recipient,
                type,
                channel,
                destination,
                resource,
                status,
                requestedsendtime,
                lastupdatetime
            FROM notifications.notificationlog
            WHERE shipmentid = @shipmentId
            LIMIT 1
            """;

        await using NpgsqlCommand cmd = DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("@shipmentId", shipmentId);

        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        int orderChainIdOrdinal = reader.GetOrdinal("orderchainid");
        int creatorNameOrdinal = reader.GetOrdinal("creatorname");
        int sendersReferenceOrdinal = reader.GetOrdinal("sendersreference");
        int dialogIdOrdinal = reader.GetOrdinal("dialogid");
        int transmissionIdOrdinal = reader.GetOrdinal("transmissionid");
        int deliveryReferenceOrdinal = reader.GetOrdinal("deliveryreference");
        int recipientOrdinal = reader.GetOrdinal("recipient");
        int destinationOrdinal = reader.GetOrdinal("destination");
        int resourceOrdinal = reader.GetOrdinal("resource");
        int statusOrdinal = reader.GetOrdinal("status");
        int requestedSendTimeOrdinal = reader.GetOrdinal("requestedsendtime");
        int lastUpdateTimeOrdinal = reader.GetOrdinal("lastupdatetime");

        return new NotificationLogEntry(
            OrderChainId: await reader.IsDBNullAsync(orderChainIdOrdinal) ? null : await reader.GetFieldValueAsync<Guid?>(orderChainIdOrdinal),
            ShipmentId: await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal("shipmentid")),
            NotificationId: await reader.GetFieldValueAsync<Guid>(reader.GetOrdinal("notificationid")),
            CreatorName: await reader.GetFieldValueAsync<string>(creatorNameOrdinal),
            SendersReference: await reader.IsDBNullAsync(sendersReferenceOrdinal) ? null : await reader.GetFieldValueAsync<string>(sendersReferenceOrdinal),
            DialogId: await reader.IsDBNullAsync(dialogIdOrdinal) ? null : await reader.GetFieldValueAsync<string>(dialogIdOrdinal),
            TransmissionId: await reader.IsDBNullAsync(transmissionIdOrdinal) ? null : await reader.GetFieldValueAsync<string>(transmissionIdOrdinal),
            DeliveryReference: await reader.IsDBNullAsync(deliveryReferenceOrdinal) ? null : await reader.GetFieldValueAsync<string>(deliveryReferenceOrdinal),
            Recipient: await reader.IsDBNullAsync(recipientOrdinal) ? null : await reader.GetFieldValueAsync<string>(recipientOrdinal),
            Type: await reader.GetFieldValueAsync<string>(reader.GetOrdinal("type")),
            Channel: await reader.GetFieldValueAsync<string>(reader.GetOrdinal("channel")),
            Destination: await reader.GetFieldValueAsync<string>(destinationOrdinal),
            Resource: await reader.IsDBNullAsync(resourceOrdinal) ? null : await reader.GetFieldValueAsync<string>(resourceOrdinal),
            Status: await reader.GetFieldValueAsync<string>(statusOrdinal),
            RequestedSendTime: await reader.GetFieldValueAsync<DateTime>(requestedSendTimeOrdinal),
            LastUpdateTime: await reader.GetFieldValueAsync<DateTime>(lastUpdateTimeOrdinal));
    }
}
