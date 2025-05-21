using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class PastDueOrdersConsumerTests : IDisposable
{
    private readonly string _pastDueOrdersTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    /// <summary>
    /// Verifies that the consumer handles new notification orders and updates their status to "Processed".
    /// </summary>
    [Fact]
    public async Task Consumer_UpdatesOrderStatusToProcessed()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__PastDueOrdersTopicName", _pastDueOrdersTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_pastDueOrdersTopicName}\"]" }
        };

        using PastDueOrdersConsumer consumerService = (PastDueOrdersConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(PastDueOrdersConsumer))!;

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        long processedOrderCount = await SelectOrderCount(orderId, "Processed");
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, processedOrderCount);
        Assert.Equal(1, emailNotificationCount);
    }

    /// <summary>
    /// Verifies that the consumer handles new notification orders and updates their status to "Completed".
    /// </summary>
    [Fact]
    public async Task Consumer_UpdatesOrderStatusToCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__PastDueOrdersTopicName", _pastDueOrdersTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_pastDueOrdersTopicName}\"]" }
        };

        using PastDueOrdersConsumer consumerRetryService = (PastDueOrdersConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(PastDueOrdersConsumer))!;

        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithSmsOrderForReservedRecipient(sendersReference: _sendersRef);

        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerRetryService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerRetryService.StopAsync(CancellationToken.None);

        // Assert
        long smsNotificationCount = await SelectSmsNotificationCount(orderId);
        long completedOrderCount = await SelectOrderCount(orderId, "Completed");

        Assert.Equal(1, completedOrderCount);
        Assert.Equal(1, smsNotificationCount);
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

    private static async Task<long> SelectSmsNotificationCount(Guid orderId)
    {
        string sql = $"select count(1) " +
                   "from notifications.smsnotifications e " +
                   "join notifications.orders o on e._orderid=o._id " +
                   $"where e._orderid = o._id and o.alternateid ='{orderId}'";
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

    private static async Task<long> SelectOrderCount(Guid orderId, string status)
    {
        string sql = $"select count(1) from notifications.orders where processedstatus = '{status}' and alternateid='{orderId}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
