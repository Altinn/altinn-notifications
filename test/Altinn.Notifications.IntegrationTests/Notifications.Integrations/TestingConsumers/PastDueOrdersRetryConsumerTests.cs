using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class PastDueOrdersRetryConsumerTests : IDisposable
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
        var processedstatus = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
         async () =>
         {
             processedstatus = await SelectProcessStatus(persistedOrder.Id);
             return processedstatus == "Processed";
         },
         TimeSpan.FromSeconds(15));

        await consumerRetryService.StopAsync(CancellationToken.None);

        Assert.Equal("Processed", processedstatus);
    }

    /// <summary>
    /// When IOrderProcessingService.ProcessOrderRetry throws an exception,
    /// we confirm that the service exception is caught and logged, and the order remains unprocessed.
    /// </summary>
    [Fact]
    public async Task RunTask_ProcessOrderRetryThrowsException_ConfirmRetryBehavior()
    {
        // Arrange
        var mockOrderProcessingService = new Mock<IOrderProcessingService>();
        mockOrderProcessingService
            .Setup(x => x.ProcessOrderRetry(It.IsAny<NotificationOrder>()))
            .ThrowsAsync(new TaskCanceledException("Simulated processing failure"));

        var mockLogger = new Mock<ILogger<PastDueOrdersRetryConsumer>>();

        using var consumerRetryService = CreateRetryConsumerService(mockLogger, mockOrderProcessingService);

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);

        await UpdateProcessingStatus(persistedOrder.Id, OrderProcessingStatus.Processing);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        // Assert - Wait for the consumer to attempt processing
        var processedOrderCount = 0L;
        var processingOrderCount = 0L;

        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                // Check if the mock was called
                try
                {
                    mockOrderProcessingService.Verify(
                        x => x.ProcessOrderRetry(It.Is<NotificationOrder>(o => o.Id == persistedOrder.Id)),
                        Times.AtLeastOnce());
                    
                    processedOrderCount = await SelectProcessedOrderCount(persistedOrder.Id);
                    processingOrderCount = await SelectProcessingOrderCount(persistedOrder.Id);
                    
                    return true; // Processing attempt completed
                }
                catch (MockException)
                {
                    return false; // Mock not called yet, keep waiting
                }
            },
            TimeSpan.FromSeconds(10));

        await consumerRetryService.StopAsync(CancellationToken.None);

        // Verify the service was called
        mockOrderProcessingService.Verify(
            x => x.ProcessOrderRetry(It.Is<NotificationOrder>(o => o.Id == persistedOrder.Id)),
            Times.AtLeastOnce());

        // Verify that the exception was logged when the TaskCanceledException is caught PastDueOrdersRetryConsumer
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exception when retrying past due order with id") 
                                       && v.ToString()!.Contains(persistedOrder.Id.ToString())),
                It.Is<Exception>(ex => ex is TaskCanceledException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "Expected logger to log TaskCanceledException on line 68 when ProcessOrderRetry throws exception");

        // Order should not be marked as Processed since the service threw an exception
        Assert.Equal(0, processedOrderCount);
        
        // Order should remain in Processing state
        Assert.Equal(1, processingOrderCount);
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
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

    private static async Task<long> SelectProcessingOrderCount(Guid orderId)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Processing' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task UpdateProcessingStatus(Guid orderId, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"UPDATE notifications.orders SET processedstatus = '{orderProcessingStatus}' WHERE alternateid='{orderId}'";
        await PostgreUtil.RunSql(sql);
    }

    private PastDueOrdersRetryConsumer CreateRetryConsumerService(
        Mock<ILogger<PastDueOrdersRetryConsumer>> mockLogger,
        Mock<IOrderProcessingService>? mockOrderProcessingService = null)
    {
        var dateTimeService = ServiceUtil
            .GetServices([typeof(IDateTimeService)])
            .OfType<IDateTimeService>()
            .First();

        var orderProcessingService = mockOrderProcessingService?.Object 
            ?? ServiceUtil.GetServices([typeof(IOrderProcessingService)])
                         .OfType<IOrderProcessingService>()
                         .First();

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
            mockLogger.Object);
    }
}
