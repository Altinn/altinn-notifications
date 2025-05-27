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

    public static async Task<(NotificationOrder Order, EmailNotification EmailNotification)> PopulateDBWithOrderAndEmailNotification(string? sendersReference = null, bool simulateCronJob = false, bool simulateConsumers = false)
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(IEmailNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        EmailNotificationRepository notificationRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

        if (sendersReference != null)
        {
            o.SendersReference = sendersReference;
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

    public static async Task<(NotificationOrder Order, SmsNotification SmsNotification)> PopulateDBWithOrderAndSmsNotification(string? sendersReference = null, SendingTimePolicy? sendingTimePolicy = null, bool simulateCronJob = false, bool simulateConsumers = false)
    {
        (NotificationOrder order, SmsNotification smsNotification) = TestdataUtil.GetOrderAndSmsNotification(sendingTimePolicy);
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(ISmsNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        SmsNotificationRepository notificationRepo = (SmsNotificationRepository)serviceList.First(i => i.GetType() == typeof(SmsNotificationRepository));

        if (sendersReference != null)
        {
            order.SendersReference = sendersReference;
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

    public static async Task DeleteOrderFromDb(string sendersRef)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;
        string sql = "DELETE FROM notifications.orders WHERE sendersreference = @sendersRef";

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);
        pgcom.Parameters.AddWithValue("@sendersRef", sendersRef);

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
