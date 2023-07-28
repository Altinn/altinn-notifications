using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;

public class PostgreUtil
{
    public static async Task<Guid> PopulateDBWithOrder()
    {
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository) });
        OrderRepository repository = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        var order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var persistedOrder = await repository.Create(order);
        return persistedOrder.Id;
    }

    public static async Task<Guid> PopulateDBWithOrderAndEmailNotification()
    {
        (NotificationOrder o, EmailNotification e) = TestdataUtil.GetOrderAndEmailNotification();
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IOrderRepository), typeof(IEmailNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        EmailNotificationRepository notificationRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

        await orderRepo.Create(o);
        await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));

        return e.Id!;
    }

    public static async Task<int> RunSqlReturnIntOutput(string query)
    {
        var builder = new ConfigurationBuilder()
           .AddJsonFile($"appsettings.json")
           .AddJsonFile("appsettings.IntegrationTest.json");

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection()
                        .AddPostgresRepositories(config);

        var serviceProvider = services.BuildServiceProvider();

        var dataSource = (NpgsqlDataSource)serviceProvider.GetServices(typeof(NpgsqlDataSource)).First()!;
        await using NpgsqlCommand pgcom = dataSource.CreateCommand(query);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();
        int count = (int)reader.GetInt64(0);

        return count;
    }

}
