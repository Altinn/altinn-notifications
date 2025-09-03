using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;
using Xunit.Sdk;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class EmailStatusConsumerTests : IAsyncLifetime
{
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();

    [Fact]
    public async Task InsertStatusFeed_OrderCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using EmailStatusConsumer emailStatusConsumer = (EmailStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is EmailStatusConsumer);

        (NotificationOrder order, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

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

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, statusFeedCount);
    }

    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer consumerService = (EmailStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is EmailStatusConsumer);

        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait for notification status to become Succeeded
        string? observedEmailStatus = null;
        await EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await SelectEmailNotificationStatus(notification.Id);
                return string.Equals(observedEmailStatus, EmailNotificationResultType.Succeeded.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Processed
        long processedOrderCount = -1;
        await EventuallyAsync(
            async () =>
            {
                processedOrderCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Processed);
                return processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await consumerService.StopAsync(CancellationToken.None);

        // Assert using captured values
        Assert.Equal(1, processedOrderCount);
        Assert.Equal(EmailNotificationResultType.Succeeded.ToString(), observedEmailStatus);
    }

    [Fact]
    public async Task InsertStatusFeed_SendersReferenceIsNull_OrderCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = (EmailStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is EmailStatusConsumer);

        (NotificationOrder order, EmailNotification notification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait until UpdateStatusAsync has executed by observing its side-effect once.
        int statusFeedCount = -1;
        await EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, statusFeedCount);

        // Cleanup
        await PostgreUtil.DeleteOrderFromDb(order.Id);
    }

    [Theory]
    [InlineData(EmailNotificationResultType.Failed)]
    [InlineData(EmailNotificationResultType.Failed_Bounced)]
    [InlineData(EmailNotificationResultType.Failed_Quarantined)]
    [InlineData(EmailNotificationResultType.Failed_FilteredSpam)]
    [InlineData(EmailNotificationResultType.Failed_RecipientReserved)]
    [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat)]
    [InlineData(EmailNotificationResultType.Failed_SupressedRecipient)]
    [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified)]
    public async Task ParseEmailSendOperationResult_StatusFailed_ShouldUpdateOrderStatusToCompleted(EmailNotificationResultType resultType)
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using EmailStatusConsumer emailStatusConsumer = (EmailStatusConsumer)ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .First(consumer => consumer is EmailStatusConsumer);

        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = resultType
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait for email notification status to be updated
        string? observedEmailStatus = null;
        await EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await SelectEmailNotificationStatus(notification.Id);
                return string.Equals(observedEmailStatus, resultType.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Completed
        long completedCount = -1;
        await EventuallyAsync(
            async () =>
            {
                completedCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Completed);
                return completedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert using captured values (no extra queries here)
        Assert.Equal(resultType.ToString(), observedEmailStatus);
        Assert.Equal(1, completedCount);
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

    /// <summary>
    /// Repeatedly evaluates a condition until it becomes <c>true</c> or a timeout is reached.
    /// </summary>
    /// <param name="predicate">An async function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>flase</c>.</param>
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

    private static async Task<string> SelectEmailNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task<long> SelectProcessingStatusOrderCount(Guid notificationId, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.emailnotifications e on e._orderid = o._id where e.alternateid = '{notificationId}' and o.processedstatus = '{orderProcessingStatus}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
