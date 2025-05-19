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
    /// When a new order is picked up by the consumer, we expect there to be an email notification created for the recipient states in the order.
    /// We measure the sucess of this test by confirming that a new email notificaiton has been created with a reference to our order id
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
        await KafkaUtil.PublishMessageOnTopic(_pastDueOrdersTopicName, persistedOrder.Serialize());

        Guid orderId = persistedOrder.Id;

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        long processedOrderCount = await SelectProcessedOrderCount(orderId);
        long emailNotificationCount = await SelectEmailNotificationCount(orderId);

        Assert.Equal(1, processedOrderCount);
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
        await KafkaUtil.DeleteTopicAsync(_pastDueOrdersTopicName);
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
}
