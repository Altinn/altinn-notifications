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
    /// Verifies that the retry consumer handles new notification orders and updates their status to "Processed".
    /// </summary>
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

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerRetryService.StopAsync(CancellationToken.None);

        // Assert
        long processedOrderCount = await SelectOrderCount(orderId, "Processed");
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, processedOrderCount);
        Assert.Equal(1, emailNotificationCount);
    }

    /// <summary>
    /// Verifies that the retry consumer handles new notification orders and updates the status to "Completed".
    /// </summary>
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

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrderForReservedRecipient(sendersReference: _sendersRef);

        await KafkaUtil.PublishMessageOnTopic(_retryTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerRetryService.StopAsync(CancellationToken.None);

        // Assert
        long completedOrderCount = await SelectOrderCount(orderId, "Completed");
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, completedOrderCount);
        Assert.Equal(1, emailNotificationCount);
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

    private static async Task<long> SelectEmailNotificationCount(Guid orderId)
    {
        string sql = $"select count(1) " +
                   "from notifications.emailnotifications e " +
                   "join notifications.orders o on e._orderid=o._id " +
                   $"where e._orderid = o._id and o.alternateid ='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<long> SelectOrderCount(Guid orderId, string status)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = '{status}' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
