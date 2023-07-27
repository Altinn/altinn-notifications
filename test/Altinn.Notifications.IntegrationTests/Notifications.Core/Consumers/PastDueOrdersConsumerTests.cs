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
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Core.Consumers;

public class PastDueOrdersConsumerTests : IDisposable
{
    private readonly string _pastDueOrdersTopicName = Guid.NewGuid().ToString();
    private IServiceProvider _serviceProvider = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// When a new order is picked up by the consumer, we expect there to be an email notification created for the recipient states in the order.
    /// We measure the sucess of this test by confirming that a new email notificaiton has been create with a reference to our order id
    /// as well as confirming that the order now has the status 'Completed' set at its processing status
    /// </summary>
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
        long completedOrderCount = await SelectCompletedOrderCount(dataSource, orderId);
        long emailNotificationCount = await SelectEmailNotificationCount(dataSource, orderId);

        Assert.Equal(1, completedOrderCount);
        Assert.Equal(1, emailNotificationCount);
    }

    public async void Dispose()
    {
        await KafkaUtil.DeleteTopicAsync(_pastDueOrdersTopicName);
        GC.SuppressFinalize(this);
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
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Completed' and alternateid='{orderId}'";
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

    private static async Task<string> PopulateDbAndTopic(IServiceProvider serviceProvider, string topicName)
    {
        var repository = (OrderRepository)serviceProvider.GetServices(typeof(IOrderRepository)).First()!;
        var persistedOrder = await repository.Create(GetOrder());

        var producer = (KafkaProducer)serviceProvider.GetServices(typeof(IKafkaProducer)).First()!;
        await producer.ProduceAsync(topicName, persistedOrder.Serialize());

        return persistedOrder.Id;
    }

    private static NotificationOrder GetOrder()
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
