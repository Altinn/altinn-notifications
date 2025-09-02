using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class EmailStatusConsumerTests : IAsyncLifetime
{
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();

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
                                                    .First(s => s.GetType() == typeof(EmailStatusConsumer))!;

        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        await EventuallyAsync(
            async () =>
            {
                string status = await SelectEmailNotificationStatus(notification.Id);
                return status == EmailNotificationResultType.Succeeded.ToString();
            }, 
            TimeSpan.FromSeconds(10));

        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        string emailNotificationStatus = await SelectEmailNotificationStatus(notification.Id);
        Assert.Equal(EmailNotificationResultType.Succeeded.ToString(), emailNotificationStatus);

        long processedOrderCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Processed);
        Assert.Equal(1, processedOrderCount);
    }

    [Theory]
    [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified)]
    [InlineData(EmailNotificationResultType.Failed_RecipientReserved)]
    [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat)]
    [InlineData(EmailNotificationResultType.Failed_SupressedRecipient)]
    [InlineData(EmailNotificationResultType.Failed_Bounced)]
    [InlineData(EmailNotificationResultType.Failed_FilteredSpam)]
    [InlineData(EmailNotificationResultType.Failed_Quarantined)]
    [InlineData(EmailNotificationResultType.Failed)]
    public async Task ParseEmailSendOperationResult_StatusFailed_ShouldUpdateOrderStatusToCompleted(EmailNotificationResultType resultType)
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using EmailStatusConsumer sut = (EmailStatusConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(EmailStatusConsumer))!;
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = resultType
        };

        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        await EventuallyAsync(
            async () =>
            {
                string status = await SelectEmailNotificationStatus(notification.Id);
                if (status != resultType.ToString())
                {
                    return false;
                }

                long completed = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Completed);
                return completed == 1;
            }, 
            TimeSpan.FromSeconds(10));

        await sut.StopAsync(CancellationToken.None);

        // Assert
        string emailNotificationStatus = await SelectEmailNotificationStatus(notification.Id);
        Assert.Equal(resultType.ToString(), emailNotificationStatus);
        long cancelledCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Completed);
        Assert.Equal(1, cancelledCount);
    }

    [Fact]
    public async Task InsertStatusFeed_OrderCompleted()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };
        using EmailStatusConsumer sut = (EmailStatusConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(EmailStatusConsumer))!;
        (NotificationOrder order, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        await EventuallyAsync(
            async () =>
            {
                int count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return count == 1;
            }, 
            TimeSpan.FromSeconds(10));

        await sut.StopAsync(CancellationToken.None);

        // Assert
        int finalCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, finalCount);
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
        using EmailStatusConsumer sut = (EmailStatusConsumer)ServiceUtil
                                                    .GetServices([typeof(IHostedService)], vars)
                                                    .First(s => s.GetType() == typeof(EmailStatusConsumer))!;
        (NotificationOrder order, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        await EventuallyAsync(
            async () =>
            {
                int count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                return count == 1;
            }, 
            TimeSpan.FromSeconds(10));

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

    private static async Task<long> SelectProcessingStatusOrderCount(Guid notificationId, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.emailnotifications e on e._orderid = o._id where e.alternateid = '{notificationId}' and o.processedstatus = '{orderProcessingStatus}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }

    private static async Task<string> SelectEmailNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(interval);
        }

        Assert.Fail("Condition not met within timeout.");
    }
}
