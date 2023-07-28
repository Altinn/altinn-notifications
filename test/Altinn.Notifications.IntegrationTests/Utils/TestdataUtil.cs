using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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
    public static async Task<string> PopulateDBWithOrder()
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
        var repository = (OrderRepository)serviceProvider.GetServices(typeof(IOrderRepository)).First()!;
        var persistedOrder = await repository.Create(TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient);
        return persistedOrder.Id;
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

    public static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient = new NotificationOrder()
    {
        Id = Guid.NewGuid().ToString(),
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
