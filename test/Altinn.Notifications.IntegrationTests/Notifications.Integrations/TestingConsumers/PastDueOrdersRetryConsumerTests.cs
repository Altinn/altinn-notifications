using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class PastDueOrdersRetryConsumerTests : IAsyncLifetime
{
    private readonly string _retryTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    /// <summary>
    /// When a new order is picked up by the consumer (this will be the retry mechanism), we expect there to be an email notification created for the recipient states in the order.
    /// We measure the success of this test by confirming that a new email notification has been created with a reference to our order id
    /// as well as confirming that the order now has the status 'Processed' set at its processing status
    /// </summary>
    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__PastDueOrdersRetryTopicName", _retryTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_retryTopicName}\"]" }
        };

        using PastDueOrdersRetryConsumer consumerRetryService = (PastDueOrdersRetryConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(PastDueOrdersRetryConsumer))!;

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);

        await UpdateProcessingStatus(persistedOrder.Id, OrderProcessingStatus.Processing);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        // Assert
        var processedOrderCount = 0L;
        var emailNotificationCount = 0L;

        await IntegrationTestUtil.EventuallyAsync(
         async () =>
         {
             processedOrderCount = await SelectProcessedOrderCount(persistedOrder.Id);
             emailNotificationCount = await SelectEmailNotificationCount(persistedOrder.Id);
             return processedOrderCount == 1 && emailNotificationCount == 1;
         },
         TimeSpan.FromSeconds(15));
        
        await consumerRetryService.StopAsync(CancellationToken.None);

        Assert.Equal(1, processedOrderCount);
        Assert.Equal(1, emailNotificationCount);
    }

    /// <summary>
    /// When a new order is picked up by the consumer and all email notifications are created before processedstatus is changed.
    /// We measure the success of this test by confirming that the processedstatus is Processed.
    /// </summary>
    [Fact]
    public async Task RunTask_ConfirmChangeOfStatus()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__PastDueOrdersRetryTopicName", _retryTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_retryTopicName}\"]" }
        };

        using PastDueOrdersRetryConsumer consumerRetryService = (PastDueOrdersRetryConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(PastDueOrdersRetryConsumer))!;

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrderAndEmailNotificationReturnOrder(sendersReference: _sendersRef);

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);

        await UpdateProcessingStatus(persistedOrder.Id, OrderProcessingStatus.Processing);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        // Assert
        var processedStatus = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
         async () =>
         {
             processedStatus = await SelectProcessStatus(persistedOrder.Id);
             return processedStatus == "Processed";
         },
         TimeSpan.FromSeconds(15));

        await consumerRetryService.StopAsync(CancellationToken.None);

        Assert.Equal("Processed", processedStatus);
    }

    /// <summary>
    /// When IOrderProcessingService.ProcessOrderRetry throws an exception,
    /// we confirm that the service exception is caught and logged, and the order is set back to status "Registered".
    /// </summary>
    [Fact]
    public async Task RunTask_ProcessOrderRetryThrowsException_ConfirmRetryBehavior()
    {
        // Arrange
        var orderRepository = ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .OfType<IOrderRepository>()
            .First();

        var mockEmailProcessingService = new Mock<IEmailOrderProcessingService>();
        var mockSmsProcessingService = new Mock<ISmsOrderProcessingService>();
        var mockPreferredChannelProcessingService = new Mock<IPreferredChannelProcessingService>();
        var mockEmailAndSmsProcessingService = new Mock<IEmailAndSmsOrderProcessingService>();
        var mockConditionClient = new Mock<IConditionClient>();
        var mockKafkaProducer = new Mock<IKafkaProducer>();
        var mockOrderProcessingLogger = new Mock<ILogger<OrderProcessingService>>();
        var mockConsumerLogger = new Mock<ILogger<PastDueOrdersRetryConsumer>>();

        // Configure mocks to throw exception when processing retry
        mockEmailProcessingService
            .Setup(x => x.ProcessOrderRetry(It.IsAny<NotificationOrder>()))
            .ThrowsAsync(new PlatformDependencyException("Profile", "Test", new TaskCanceledException()));

        var orderProcessingService = new OrderProcessingService(
            orderRepository,
            mockEmailProcessingService.Object,
            mockSmsProcessingService.Object,
            mockPreferredChannelProcessingService.Object,
            mockEmailAndSmsProcessingService.Object,
            mockConditionClient.Object,
            mockKafkaProducer.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                PastDueOrdersTopicName = "past-due-orders"
            }),
            mockOrderProcessingLogger.Object);

        using var consumerRetryService = CreateRetryConsumerService(orderProcessingService, mockConsumerLogger);

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);

        await UpdateProcessingStatus(persistedOrder.Id, OrderProcessingStatus.Processing);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        // Assert - Wait for the consumer to attempt processing
        var registeredOrderCount = 0L;

        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                registeredOrderCount = await SelectRegisteredOrderCount(persistedOrder.Id);
                
                // Check if the mock was called
                try
                {
                    mockEmailProcessingService.Verify(
                        x => x.ProcessOrderRetry(It.Is<NotificationOrder>(o => o.Id == persistedOrder.Id)),
                        Times.AtLeastOnce());
                    return registeredOrderCount == 1;
                }
                catch (MockException)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10));

        await consumerRetryService.StopAsync(CancellationToken.None);

        // Verify the mock was called
        mockEmailProcessingService.Verify(
            x => x.ProcessOrderRetry(It.Is<NotificationOrder>(o => o.Id == persistedOrder.Id)),
            Times.AtLeastOnce());

        // Verify that OrderProcessingService logged the PlatformDependencyException
        mockOrderProcessingLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Platform dependency")),
                It.Is<Exception>(ex => ex is PlatformDependencyException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "Expected OrderProcessingService to log PlatformDependencyException");

        // Verify that the order status was set back to "Registered"
        Assert.Equal(1, registeredOrderCount);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await KafkaUtil.DeleteTopicAsync(_retryTopicName);
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
    }

    private static async Task<string> SelectProcessStatus(Guid orderId)
    {
        string sql = $"select processedstatus from notifications.orders where alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
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

    private static async Task<long> SelectRegisteredOrderCount(Guid orderId)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Registered' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task UpdateProcessingStatus(Guid orderId, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"UPDATE notifications.orders SET processedstatus = '{orderProcessingStatus}' WHERE alternateid='{orderId}'";
        await PostgreUtil.RunSql(sql);
    }

    private PastDueOrdersRetryConsumer CreateRetryConsumerService(
        IOrderProcessingService? ops = null,
        Mock<ILogger<PastDueOrdersRetryConsumer>>? mockLogger = null)
    {
        var dateTimeService = ServiceUtil
            .GetServices([typeof(IDateTimeService)])
            .OfType<IDateTimeService>()
            .First();

        var orderProcessingService = ops 
            ?? ServiceUtil.GetServices([typeof(IOrderProcessingService)])
                         .OfType<IOrderProcessingService>()
                         .First();

        var logger = mockLogger?.Object ?? NullLogger<PastDueOrdersRetryConsumer>.Instance;

        return new PastDueOrdersRetryConsumer(
            orderProcessingService,
            dateTimeService,
            Options.Create(new Altinn.Notifications.Integrations.Configuration.KafkaSettings
            {
                PastDueOrdersRetryTopicName = _retryTopicName,
                BrokerAddress = "localhost:9092",
                Consumer = new Altinn.Notifications.Integrations.Configuration.ConsumerSettings
                {
                    GroupId = "test-group"
                },
                Admin = new Altinn.Notifications.Integrations.Configuration.AdminSettings
                {
                    TopicList = [_retryTopicName]
                }
            }),
            logger);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Dispose(true);
    }
}
