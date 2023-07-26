using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Integrations.Consumers;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Core.Consumers;

public class PastDueOrdersConsumerTests : IAsyncDisposable
{
    private readonly string _pastDueOrdersTopicName = Guid.NewGuid().ToString();
    private IServiceProvider _serviceProvider = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        _serviceProvider = SetUpServices(_pastDueOrdersTopicName);

        string orderId = await PopulateDbAndTopic(_serviceProvider, _pastDueOrdersTopicName);

        var consumerService = _serviceProvider
            .GetServices<IHostedService>()
            .First(s => s.GetType() == typeof(PastDueOrdersConsumer));

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        var dataSource = (NpgsqlDataSource)_serviceProvider.GetServices(typeof(NpgsqlDataSource)).First()!;
        long completedOrderCound = await SelectCompletedOrderCount(dataSource, orderId);
        long emailNotificationCount = await SelectEmailNotificationCount(dataSource, orderId);



        Assert.Equal(1, completedOrderCound);
        Assert.Equal(1, emailNotificationCount);
    }

    public async ValueTask DisposeAsync()
    {
        await DeleteTopic(_serviceProvider, _pastDueOrdersTopicName);
    }

    private static IServiceProvider SetUpServices(string topicName)
    {
        Environment.SetEnvironmentVariable("KafkaSettings__PastDueOrdersTopicName", topicName);
        Environment.SetEnvironmentVariable("KafkaSettings__TopicList", $"[\"{topicName}\"]");

        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables();

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddCoreServices(config)
            .AddPostgresRepositories(config)
            .AddKafkaServices(config);

        return services.BuildServiceProvider();
    }

    private static async Task<long> SelectCompletedOrderCount(NpgsqlDataSource dataSource, string orderId)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = 'completed' and alternateid='{orderId}'";
        return await RunSqlReturnCount(dataSource, sql);
    }

    private static async Task<long> SelectEmailNotificationCount(NpgsqlDataSource dataSource, string orderId)
    {
        string sql = $"select count(1) " +
                   "from notifications.emailnotifications e " +
                   "join notifications.orders o on e._orderid=o._id " +
                   $"where e._orderid = o._id and o.alternateid ='{orderId}'";
        return await RunSqlReturnCount(dataSource, sql);
    }

    private static async Task<int> RunSqlReturnCount(NpgsqlDataSource dataSource, string sql)
    {
        await using NpgsqlCommand pgcom = dataSource.CreateCommand(sql);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (int)reader.GetInt64(0);
    }

    private async Task<string> PopulateDbAndTopic(IServiceProvider serviceProvider, string topicName)
    {
        var repository = (OrderRepository)serviceProvider.GetServices(typeof(IOrderRepository)).First()!;
        var persistedOrder = await repository.Create(GetOrder());

        var producer = (KafkaProducer)serviceProvider.GetServices(typeof(IKafkaProducer)).First()!;
        await producer.ProduceAsync(topicName, persistedOrder.Serialize());

        return persistedOrder.Id;
    }

    private async Task DeleteTopic(IServiceProvider serviceProvider, string topicName)
    {
        var producer = (KafkaProducer)serviceProvider.GetServices(typeof(IKafkaProducer)).First()!;
        await producer.DeleteTopicAsync(topicName);
    }
    private NotificationOrder GetOrder()
    {
        return new NotificationOrder()
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
}
