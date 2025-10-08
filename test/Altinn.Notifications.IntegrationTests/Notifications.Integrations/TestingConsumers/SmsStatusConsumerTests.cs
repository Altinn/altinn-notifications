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
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();

    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using SmsStatusConsumer smsStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<SmsStatusConsumer>()
            .First();

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Accepted
        };

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait for SMS notification status to become Accepted
        string? observedSmsStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedSmsStatus = await SelectSmsNotificationStatus(notification.Id);
                return string.Equals(observedSmsStatus, SmsNotificationResultType.Accepted.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Processed
        long processedOrderCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedOrderCount = await SelectProcessedOrderCount(notification.Id);
                return processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(SmsNotificationResultType.Accepted.ToString(), observedSmsStatus);
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

        using SmsStatusConsumer smsStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<SmsStatusConsumer>()
            .First();

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true, simulateConsumers: true);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, string.Empty);

        // Wait until order is processed, capture status once when it happens
        long processedOrderCount = -1;
        string? observedSmsStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedOrderCount = await SelectProcessedOrderCount(notification.Id);

                if (processedOrderCount == 1)
                {
                    observedSmsStatus = await SelectSmsNotificationStatus(notification.Id);
                    return true;
                }

                return false;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processedOrderCount);
        Assert.Equal(SmsNotificationResultType.New.ToString(), observedSmsStatus);
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

        using SmsStatusConsumer smsStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<SmsStatusConsumer>()
            .First();

        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        int statusFeedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, statusFeedCount);
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

        using SmsStatusConsumer smsStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<SmsStatusConsumer>()
            .First();

        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        int statusFeedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, statusFeedCount);

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
