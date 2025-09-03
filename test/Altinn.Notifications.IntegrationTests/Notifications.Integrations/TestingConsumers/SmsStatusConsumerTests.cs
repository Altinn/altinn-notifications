using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;
using Microsoft.Extensions.Hosting;

using Xunit;
using Xunit.Sdk;

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

        using SmsStatusConsumer consumerService = (SmsStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is SmsStatusConsumer);

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Accepted
        };

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait for SMS notification status to become Accepted
        string? observedSmsStatus = null;
        await EventuallyAsync(
            async () =>
            {
                observedSmsStatus = await SelectSmsNotificationStatus(notification.Id);
                return string.Equals(observedSmsStatus, SmsNotificationResultType.Accepted.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Processed
        long processedOrderCount = -1;
        await EventuallyAsync(
            async () =>
            {
                processedOrderCount = await SelectProcessedOrderCount(notification.Id);
                return processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await consumerService.StopAsync(CancellationToken.None);

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

        using SmsStatusConsumer consumerService = (SmsStatusConsumer)ServiceUtil
            .GetServices(new List<Type>() { typeof(IHostedService) }, vars)
            .First(consumer => consumer is SmsStatusConsumer);

        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true, simulateConsumers: true);

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, string.Empty);

        // Wait until order is processed, capture status once when it happens
        long processedOrderCount = -1;
        string? observedSmsStatus = null;
        await EventuallyAsync(
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

        await consumerService.StopAsync(CancellationToken.None);

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
        using SmsStatusConsumer sut = (SmsStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is SmsStatusConsumer);

        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(_sendersRef, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        // Act
        await sut.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        int statusFeedCount = -1;
        await EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await sut.StopAsync(CancellationToken.None);

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
        using SmsStatusConsumer sut = (SmsStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is SmsStatusConsumer);

        (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        // Act
        await sut.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        int statusFeedCount = -1;
        await EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await sut.StopAsync(CancellationToken.None);

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

    /// <summary>
    /// Repeatedly evaluates a condition until it becomes true or a timeout is reached.
    /// </summary>
    /// <param name="predicate">An async function that evaluates the condition to be met. Returns true if the condition is satisfied, otherwise false.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    private static async Task EventuallyAsync(Func<Task<bool>> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        var deadline = DateTime.UtcNow.Add(maximumWaitTime);
        var pollingInterval = checkInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(pollingInterval);
        }

        throw new XunitException($"Condition not met within timeout ({maximumWaitTime}).");
    }
}
