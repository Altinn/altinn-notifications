using System.Net;
using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;
public static class TestdataUtil
{
    public static async Task<Guid> PopulateDBWithOrder()
    {
        var serviceList = GetServices(new List<Type>() { typeof(IOrderRepository) });
        OrderRepository repository = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        var order = NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var persistedOrder = await repository.Create(order);
        return persistedOrder.Id;
    }

    public static async Task<Guid> PopulateDBWithOrderAndEmailNotification()
    {
        (NotificationOrder o, EmailNotification e) = GetOrderAndEmailNotification();
        var serviceList = GetServices(new List<Type>() { typeof(IOrderRepository), typeof(IEmailNotificationRepository) });

        OrderRepository orderRepo = (OrderRepository)serviceList.First(i => i.GetType() == typeof(OrderRepository));
        EmailNotificationRepository notificationRepo = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

        await orderRepo.Create(o);
        await notificationRepo.AddNotification(e, DateTime.UtcNow.AddDays(1));

        return e.Id!;
    }

    private static List<object> GetServices(List<Type> interfaceTypes)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json");

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddPostgresRepositories(config);

        var serviceProvider = services.BuildServiceProvider();
        List<object> outputServices = new();

        foreach (Type interfaceType in interfaceTypes)
        {
            object outputServiceObject = serviceProvider.GetServices(interfaceType).First()!;
            outputServices.Add(outputServiceObject);
        }

        return outputServices;
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

    public static (NotificationOrder order, EmailNotification notification) GetOrderAndEmailNotification()
    {
        NotificationOrder order = NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var recipient = order.Recipients.First();
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        var emailNotification = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = order.RequestedSendTime,
            ToAddress = addressPoint!.EmailAddress,
            RecipientId = recipient.RecipientId,
            SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow)
        };

        return (order, emailNotification);
    }

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    private static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            SendersReference = "senders-reference",
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate()
                {
                    Type = NotificationTemplateType.Email,
                    FromAddress = "sender@domain.com",
                    Subject = "email-subject",
                    Body = "email-body",
                    ContentType = EmailContentType.Html
                }
            },
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Email,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
                    RecipientId = "recipient1",
                    AddressInfo = new()
                    {
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "recipient1@domain.com"
                        }
                    }
                }
            }
        };
    }
}
