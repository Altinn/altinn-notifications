using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Mappers;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class PastDueOrdersConsumerTests : IDisposable
{
    private readonly string _pastDueOrdersTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    /// <summary>
    /// When a new order is picked up by the consumer, we expect there to be an email notification created for the recipient states in the order.
    /// We measure the success of this test by confirming that a new email notification has been created with a reference to our order id
    /// as well as confirming that the order now has the status 'Processed' set at its processing status
    /// </summary>
    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__PastDueOrdersTopicName", _pastDueOrdersTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_pastDueOrdersTopicName}\"]" }
        };

        using PastDueOrdersConsumer consumerService = (PastDueOrdersConsumer)ServiceUtil
                                                    .GetServices(new List<Type>() { typeof(IHostedService) }, vars)
                                                    .First(s => s.GetType() == typeof(PastDueOrdersConsumer))!;

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        // Act
        await consumerService.StartAsync(CancellationToken.None);

        await UpdateProcessingStatus(persistedOrder.Id, OrderProcessingStatus.Processing);

        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, persistedOrder.Serialize());

        // Assert
        var selectProcessedOrderCount = 0L;
        var selectEmailNotificationCount = 0L;
        await IntegrationTestUtil.EventuallyAsync(
          async () =>
          {
              selectProcessedOrderCount = await SelectProcessedOrderCount(persistedOrder.Id);
              selectEmailNotificationCount = await SelectEmailNotificationCount(persistedOrder.Id);
              return selectProcessedOrderCount == 1 && selectEmailNotificationCount == 1;
          },
          TimeSpan.FromSeconds(15));

        await consumerService.StopAsync(CancellationToken.None);
        Assert.Equal(1L, selectProcessedOrderCount);
        Assert.Equal(1L, selectEmailNotificationCount);
    }

    [Theory]
    [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified)]
    [InlineData(EmailNotificationResultType.Failed_RecipientReserved)]
    public async Task ProcessOrder_WithEmailRecipientNotIdentified_ShouldCreateStatusFeedEntry(EmailNotificationResultType status)
    {
        // Arrange
        using var consumerService = CreateConsumerService();
        (NotificationOrder order, _) = await SetupOrderWithFailedEmailRecipient(status);

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, order.Serialize());

        // Assert
        var lifecycleStatus = ProcessingLifecycleMapper.GetEmailLifecycleStage(status.ToString());
        await AssertStatusFeedEntryCreated(order.Id, lifecycleStatus);
        await consumerService.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(SmsNotificationResultType.Failed_RecipientNotIdentified)]
    [InlineData(SmsNotificationResultType.Failed_RecipientReserved)]
    public async Task ProcessOrder_WithSmsRecipientNotIdentified_ShouldCreateStatusFeedEntry(SmsNotificationResultType status)
    {
        // Arrange
        using var consumerService = CreateConsumerService();
        (NotificationOrder order, _) = await SetupOrderWithFailedSmsRecipient(status);

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, order.Serialize());

        // Assert
        var lifecycleStatus = ProcessingLifecycleMapper.GetSmsLifecycleStage(status.ToString());
        await AssertStatusFeedEntryCreated(order.Id, lifecycleStatus);
        await consumerService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RetryOrder_WhenProducerReturnsFalse_ThrowsInvalidOperationException()
    {
        // Arrange
        var logger = new Mock<ILogger<PastDueOrdersConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Strict);
        var orderProcessingService = new Mock<IOrderProcessingService>();
        
        var pastDueOrdersRetryTopicName = Guid.NewGuid().ToString();
        await KafkaUtil.CreateTopicAsync(_pastDueOrdersTopicName);
        await KafkaUtil.CreateTopicAsync(pastDueOrdersRetryTopicName);

        try
        {
            var kafkaSettings = Options.Create(new Altinn.Notifications.Integrations.Configuration.KafkaSettings
            {
                PastDueOrdersTopicName = _pastDueOrdersTopicName,
                PastDueOrdersRetryTopicName = pastDueOrdersRetryTopicName,
                BrokerAddress = "localhost:9092",
                Consumer = new Altinn.Notifications.Integrations.Configuration.ConsumerSettings 
                { 
                    GroupId = $"altinn-notifications-{Guid.NewGuid():N}" 
                },
                Admin = new Altinn.Notifications.Integrations.Configuration.AdminSettings
                {
                    TopicList = [_pastDueOrdersTopicName, pastDueOrdersRetryTopicName]
                }
            });

            // Setup order processing to throw an exception, which will trigger the retry mechanism
            orderProcessingService
                .Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()))
                .ThrowsAsync(new Exception("Simulated processing failure"));

            // Setup producer to return false on retry (this simulates the retry publish failure)
            kafkaProducer
                .Setup(p => p.ProduceAsync(pastDueOrdersRetryTopicName, It.IsAny<string>()))
                .ReturnsAsync(false);

            using var pastDueOrdersConsumer = new PastDueOrdersConsumer(
                kafkaProducer.Object,
                kafkaSettings,
                logger.Object,
                orderProcessingService.Object);

            var order = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);
            var orderMessage = order.Serialize();

            // Act
            await pastDueOrdersConsumer.StartAsync(CancellationToken.None);
            await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, orderMessage);

            // Assert
            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    try
                    {
                        // Verify ProcessOrder was called once (and threw exception)
                        orderProcessingService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

                        // Verify producer was called once with the retry message and returned false
                        kafkaProducer.Verify(p => p.ProduceAsync(pastDueOrdersRetryTopicName, orderMessage), Times.Once);

                        // Verify InvalidOperationException was logged
                        logger.Verify(
                            l => l.Log(
                                LogLevel.Error,
                                It.IsAny<EventId>(),
                                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to republish message to topic") && 
                                                               v.ToString()!.Contains(pastDueOrdersRetryTopicName)),
                                It.IsAny<InvalidOperationException>(),
                                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                            Times.Once);

                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15));

            await pastDueOrdersConsumer.StopAsync(CancellationToken.None);
        }
        finally
        {
            await KafkaUtil.DeleteTopicAsync(pastDueOrdersRetryTopicName);
        }
    }

    private async Task<(NotificationOrder Order, SmsNotification Notification)> SetupOrderWithFailedSmsRecipient(SmsNotificationResultType status)
    {
        var (o, n) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(
            sendersReference: _sendersRef,
            simulateCronJob: true,
            simulateConsumers: true);

        await PostgreUtil.UpdateNotificationResult<SmsNotification>(o.Id, status.ToString());
        return (o, n);
    }

    private PastDueOrdersConsumer CreateConsumerService()
    {
        var orderRepository = ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .OfType<OrderRepository>()
            .First();

        var orderProcessingService = new OrderProcessingService(
            orderRepository,
            new Mock<IEmailOrderProcessingService>().Object,
            new Mock<ISmsOrderProcessingService>().Object,
            new Mock<IPreferredChannelProcessingService>().Object,
            new Mock<IEmailAndSmsOrderProcessingService>().Object,
            new Mock<IConditionClient>().Object,
            new Mock<IKafkaProducer>().Object,
            Options.Create(new KafkaSettings { PastDueOrdersTopicName = _pastDueOrdersTopicName }),
            NullLogger<OrderProcessingService>.Instance);

        return new PastDueOrdersConsumer(
            new Mock<IKafkaProducer>().Object,
            Options.Create(new Altinn.Notifications.Integrations.Configuration.KafkaSettings
            {
                PastDueOrdersTopicName = _pastDueOrdersTopicName,
                BrokerAddress = "localhost:9092",
                Admin = new Altinn.Notifications.Integrations.Configuration.AdminSettings
                {
                    TopicList = [_pastDueOrdersTopicName]
                }
            }),
            NullLogger<PastDueOrdersConsumer>.Instance,
            orderProcessingService);
    }

    private async Task<(NotificationOrder Order, EmailNotification Notification)> SetupOrderWithFailedEmailRecipient(EmailNotificationResultType status)
    {
        var (o, n) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(
            sendersReference: _sendersRef,
            simulateCronJob: true,
            simulateConsumers: true);

        await PostgreUtil.UpdateNotificationResult<EmailNotification>(o.Id, status.ToString());
        return (o, n);
    }

    private static async Task AssertStatusFeedEntryCreated(Guid orderId, ProcessingLifecycle expectedStatus)
    {
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(orderId);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15));

        var statusFeedEntry = await GetStatusFeedOrderStatus(orderId);
        Assert.NotNull(statusFeedEntry);
        Assert.Contains(statusFeedEntry.Recipients, x => x.Status == expectedStatus);
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_pastDueOrdersTopicName);
    }

    private static async Task<long> SelectProcessedOrderCount(Guid orderId)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Processed' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<long> SelectEmailNotificationCount(Guid orderId)
    {
        string sql = $"select count(1) from notifications.emailnotifications e join notifications.orders o on e._orderid = o._id where o.alternateid ='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task UpdateProcessingStatus(Guid orderId, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"UPDATE notifications.orders SET processedstatus = '{orderProcessingStatus}' WHERE alternateid='{orderId}'";
        await PostgreUtil.RunSql(sql);
    }

    private static async Task<OrderStatus?> GetStatusFeedOrderStatus(Guid orderId)
    {
        var json = await PostgreUtil.GetStatusFeedOrderStatusJson(orderId);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OrderStatus>(json, JsonSerializerOptionsProvider.Options);
    }
}
