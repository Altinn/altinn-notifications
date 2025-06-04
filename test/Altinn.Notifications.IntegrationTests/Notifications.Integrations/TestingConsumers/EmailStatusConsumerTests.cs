using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
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
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
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
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());
        
        // Act
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await sut.StopAsync(CancellationToken.None);
        
        // Assert
        string emailNotificationStatus = await SelectEmailNotificationStatus(notification.Id);
        Assert.Equal(resultType.ToString(), emailNotificationStatus);
        long cancelledCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Completed);
        Assert.Equal(1, cancelledCount);
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
}
