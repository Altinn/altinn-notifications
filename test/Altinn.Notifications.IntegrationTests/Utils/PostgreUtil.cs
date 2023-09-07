using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class PostgreUtil
{
    public static async Task<Guid> PopulateDBWithOrderAndReturnId(string? sendersReference = null)
    {
        var order = await PopulateDBWithOrder(sendersReference);
        return order.Id;
    }

    public static async Task<NotificationOrder> PopulateDBWithOrder(string? sendersReference = null)
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

    public static async Task<(NotificationOrder Order, EmailNotification EmailNotification)>
        PopulateDBWithOrderAndEmailNotification(string? sendersReference = null)
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

    public static async Task<int> RunSqlReturnIntOutput(string query)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();
        int count = (int)reader.GetInt64(0);

        return count;
    }

    public static async Task<string> RunSqlReturnStringOutput(string query)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        string result = reader.GetString(0);

        return result;
    }

    public static async Task RunSql(string query)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices(new List<Type>() { typeof(NpgsqlDataSource) })[0]!;

        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);
        pgcom.ExecuteNonQuery();
    }
}