using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class SmsStatusConsumerTests : IAsyncLifetime
{
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using SmsStatusConsumer consumerService = (SmsStatusConsumer)ServiceUtil
                                                  .GetServices([typeof(IHostedService)], vars)
                                                  .First(s => s.GetType() == typeof(SmsStatusConsumer))!;

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Accepted
        };

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        string smsNotificationStatus = await SelectSmsNotificationStatus(notification.Id);
        Assert.Equal(SmsNotificationResultType.Accepted.ToString(), smsNotificationStatus);

        long processedOrderCount = await SelectProcessedOrderCount(notification.Id);
        Assert.Equal(1, processedOrderCount);
    }

    [Fact]
    public async Task RunTask_ParseSmsSendOperationResult_StatusNotUpdated()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using SmsStatusConsumer consumerService = (SmsStatusConsumer)ServiceUtil
                                            .GetServices(new List<Type>() { typeof(IHostedService) }, vars)
                                            .First(s => s.GetType() == typeof(SmsStatusConsumer))!;

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true, simulateConsumers: true);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, string.Empty);

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        string smsNotificationStatus = await SelectSmsNotificationStatus(notification.Id);
        Assert.Equal(SmsNotificationResultType.New.ToString(), smsNotificationStatus);

        long processedOrderCount = await SelectProcessedOrderCount(notification.Id);
        Assert.Equal(1, processedOrderCount);
    }

    [Fact]
    public async Task InsertStatusFeed_OrderCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using SmsStatusConsumer sut = (SmsStatusConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(SmsStatusConsumer))!;
        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);
        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        int count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InsertStatusFeed_SendersReferenceIsNull_OrderCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using SmsStatusConsumer sut = (SmsStatusConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(SmsStatusConsumer))!;
        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);
        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        int count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, count);

        // cleanup
        await PostgreUtil.DeleteOrderFromDb(order.Id);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Dispose(true);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await PostgreUtil.DeleteStatusFeedFromDb(_sendersRef);
        await PostgreUtil.DeleteNotificationsFromDb(_sendersRef);
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedTopicName);
    }

    private static async Task<long> SelectProcessedOrderCount(Guid notificationId)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.smsnotifications e on e._orderid = o._id where e.alternateid = '{notificationId}' and o.processedstatus = '{OrderProcessingStatus.Processed}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<string> SelectSmsNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.smsnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }
}
