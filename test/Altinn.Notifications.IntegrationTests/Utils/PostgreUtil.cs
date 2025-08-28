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
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository) });
        OrderRepository repository = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        var persistedOrder = await repository.Create(order);
        return persistedOrder;
    }

    public static async Task<NotificationOrder> PopulateDBWithSmsOrder(string? sendersReference = null)
    {
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository) });
        OrderRepository repository = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        var order = TestdataUtil.NotificationOrder_SmsTemplate_OneRecipient();
        order.Id = Guid.NewGuid();

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        var persistedOrder = await repository.Create(order);
        return persistedOrder;
    }

    public static async Task<(NotificationOrder Order, EmailNotification EmailNotification)> PopulateDBWithOrderAndEmailNotification(string? sendersReference = null, bool simulateCronJob = false, bool simulateConsumers = false, bool forceSendersReferenceToBeNull = false)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(IEmailNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        EmailNotificationRepository notificationRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

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
            await orderRepo.Create(o);
            await orderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processing);
            await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
            await orderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processed);
        }
        else if (simulateCronJob && !simulateConsumers)
        {
            await orderRepo.Create(o);
            await orderRepo.SetProcessingStatus(o.Id, OrderProcessingStatus.Processing);
            await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
        }
        else
        {
            await orderRepo.Create(o);
            await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));
        }

        return (o, e);
    }

    public static async Task<NotificationOrder> PopulateDBWithOrderAndEmailNotificationReturnOrder(string? sendersReference = null)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(IEmailNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        EmailNotificationRepository notificationRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

        if (sendersReference != null)
        {
            o.SendersReference = sendersReference;
        }

        await orderRepo.Create(o);
        await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));

        return o;
    }

    public static async Task<(NotificationOrder Order, SmsNotification SmsNotification)> PopulateDBWithOrderAndSmsNotification(string? sendersReference = null, SendingTimePolicy? sendingTimePolicy = null, bool simulateCronJob = false, bool simulateConsumers = false, bool forceSendersReferenceToBeNull = false)
    {
        (NotificationOrder order, SmsNotification smsNotification) = TestdataUtil.GetOrderAndSmsNotification(sendingTimePolicy);
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(ISmsNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        SmsNotificationRepository notificationRepo = (SmsNotificationRepository)serviceList.First(i => i.GetType() == typeof(SmsNotificationRepository));

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
        }

        if (forceSendersReferenceToBeNull)
        {
            // Force the senders reference to be null, even if a value is provided
            order.SendersReference = null;
        }

        /*
        * Notes:
        * 1. When a new notification order is created in the database, its processing status is 'Registered'.
        * 2. When handling of a registered order begins, its processing status should be updated to 'Processing'.
        * 3. Once handling of a notification order in the 'Processing' state is done, its processing status should be updated to 'Processed'.
        */
        if (simulateCronJob && simulateConsumers)
        {
            await orderRepo.Create(order);
            await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
            await notificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
            await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);
        }
        else if (simulateCronJob && !simulateConsumers)
        {
            await orderRepo.Create(order);
            await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
            await notificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
        }
        else
        {
            await orderRepo.Create(order);
            await notificationRepo.AddNotification(smsNotification, DateTime.UtcNow.AddDays(1), 1);
        }

        return (order, smsNotification);
    }

    public static async Task<NotificationOrder> PopulateDBWithOrderAnd4Notifications(string orgName)
    {
        // Get test data for base order with one notification
        (NotificationOrder order, SmsNotification smsNotification1) = TestdataUtil.GetOrderAndSmsNotification();
        order.Creator = new Core.Models.Creator(orgName);
        var timeStamp = DateTime.UtcNow;
        order.RequestedSendTime = timeStamp;

        var smsNotification2 = new SmsNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                MobileNumber = smsNotification1.Recipient.MobileNumber,
            },
            SendResult = new(SmsNotificationResultType.New, timeStamp)
        };

        var emailNotification1 = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                ToAddress = "noreply@altinn.no"
            }
        };

        var emailNotification2 = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = timeStamp,
            Recipient = new()
            {
                ToAddress = "noreply@altinn.no"
            }
        };

        // Use the SMS order as the base and ensure all 4 notifications reference the same order
        emailNotification1.OrderId = order.Id;
        emailNotification2.OrderId = order.Id;
        smsNotification1.OrderId = order.Id;
        smsNotification2.OrderId = order.Id;

        // Set up repositories
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(ISmsNotificationRepository), typeof(IEmailNotificationRepository) });
        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        SmsNotificationRepository smsRepo = (SmsNotificationRepository)serviceList.First(i => i.GetType() == typeof(SmsNotificationRepository));
        EmailNotificationRepository emailRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        await smsRepo.AddNotification(smsNotification1, DateTime.UtcNow.AddDays(1), 1);
        await smsRepo.AddNotification(smsNotification2, DateTime.UtcNow.AddDays(1), 1);
        await emailRepo.AddNotification(emailNotification1, DateTime.UtcNow.AddDays(1));
        await emailRepo.AddNotification(emailNotification2, DateTime.UtcNow.AddDays(1));

        await emailRepo.UpdateSendStatus(emailNotification1.Id, EmailNotificationResultType.Delivered);
        await emailRepo.UpdateSendStatus(emailNotification2.Id, EmailNotificationResultType.Delivered);

        await smsRepo.UpdateSendStatus(smsNotification1.Id, SmsNotificationResultType.Accepted);
        await smsRepo.UpdateSendStatus(smsNotification2.Id, SmsNotificationResultType.Accepted);

        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);

        return order;
    }

    public static async Task DeleteOrderFromDb(string sendersRef)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;
        string sql = "DELETE FROM notifications.orders WHERE sendersreference = @sendersRef";

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("@sendersRef", sendersRef);

        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task DeleteOrderFromDb(Guid id)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;
        string sql = "DELETE FROM notifications.orders WHERE alternateid = @id";

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);
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
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;
        string sql = @"DELETE FROM notifications.statusfeed s
                       USING notifications.orders o
                       WHERE s.orderid = o._id AND o.sendersreference = @sendersRef;";
        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("sendersRef", sendersRef);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task DeleteNotificationsFromDb(string sendersRef)
    {         
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;
        string sql = @"
                        BEGIN;

                        -- First, delete from SMS notifications
                        DELETE FROM notifications.smsnotifications s
                        USING notifications.orders o
                        WHERE s._orderid = o._id
                          AND o.sendersreference = @sendersRef;

                        -- Second, delete from Email notifications
                        DELETE FROM notifications.emailnotifications e
                        USING notifications.orders o
                        WHERE e._orderid = o._id
                          AND o.sendersreference = @sendersRef;

                        COMMIT;";
        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("sendersRef", sendersRef);
        await pgcom.ExecuteNonQueryAsync();
    }

    public static async Task<T> RunSqlReturnOutput<T>(string query)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        T result = reader.GetValue<T>(0);
        return result;
    }

    public static async Task RunSql(string query)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);
        await pgcom.ExecuteNonQueryAsync();
    }
}
