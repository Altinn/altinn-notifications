using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class PastDueOrdersRetryConsumerTests : IDisposable
{
    private readonly string _retryTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    /// <summary>
    /// Verifies that the retry consumer creates email notifications and updates
    /// the order status to "Processed" after the email service has processed the email notification.
    /// </summary>
    /// <remarks>
    /// This test:
    /// 1. Creates an email notification order in the database
    /// 2. Publishes this order to the retry topic for processing
    /// 3. Verifies that email notification is created for the recipients in the order
    /// 4. Confirms that the order's processing status is updated to "Processed" because the email notification has not reached final state yet
    /// </remarks>
    [Fact]
    public async Task RetryConsumer_UpdatesOrderStatusToProcessed()
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

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerRetryService.StopAsync(CancellationToken.None);

        // Assert
        string processedstatus = await SelectProcessStatus(orderId);
        long processedOrderCount = await SelectProcessedOrderCount(orderId);
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, processedOrderCount);
        Assert.Equal(1, emailNotificationCount);
        Assert.Equal("Processed", processedstatus);
    }

    /// <summary>
    /// Verifies that the retry consumer creates email notifications and updates
    /// the order status to "Completed" after the email service has processed the email notification.
    /// </summary>
    /// <remarks>
    /// This test:
    /// 1. Creates an email notification order in the database
    /// 2. Publishes this order to the retry topic for processing
    /// 3. Verifies that email notification is created for the recipients in the order
    /// 4. Confirms that the order's processing status is updated to "Completed" because the email notification has reached final state yet
    /// </remarks>
    [Fact]
    public async Task RetryConsumer_UpdatesOrderStatusToCompleted()
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

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrderAndEmailNotificationForReservedRecipient(sendersReference: _sendersRef);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerRetryService.StopAsync(CancellationToken.None);

        // Assert
        string processedstatus = await SelectProcessStatus(orderId);
        long processedOrderCount = await SelectProcessedOrderCount(orderId);
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, processedOrderCount);
        Assert.Equal(1, emailNotificationCount);
        Assert.Equal("Completed", processedstatus);
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_retryTopicName);
    }

    private static async Task<long> SelectProcessedOrderCount(Guid orderId)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Processed' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<long> SelectEmailNotificationCount(Guid orderId)
    {
        string sql = $"select count(1) " +
                   "from notifications.emailnotifications e " +
                   "join notifications.orders o on e._orderid=o._id " +
                   $"where e._orderid = o._id and o.alternateid ='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<string> SelectProcessStatus(Guid orderId)
    {
        string sql = $"select processedstatus from notifications.orders where alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }
}
