using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<EmailStatusConsumer>()
            .First();

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
        await IntegrationTestUtil.EventuallyAsync(
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

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<EmailStatusConsumer>()
            .First();

        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());

        // Wait for notification status to become Succeeded
        string? observedEmailStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await SelectEmailNotificationStatus(notification.Id);
                return string.Equals(observedEmailStatus, EmailNotificationResultType.Succeeded.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Processed
        long processedOrderCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedOrderCount = await SelectProcessingStatusOrderCount(notification.Id, OrderProcessingStatus.Processed);
                return processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

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

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<EmailStatusConsumer>()
            .First();

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
        await IntegrationTestUtil.EventuallyAsync(
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

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], vars)
            .OfType<EmailStatusConsumer>()
            .First();

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
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await SelectEmailNotificationStatus(notification.Id);
                return string.Equals(observedEmailStatus, resultType.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        // Then wait for order processing status to reach Completed
        long completedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
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

    [Fact]
    public async Task ProcessStatus_ServiceThrowsSendStatusUpdateException_ShouldPublishToRetryTopic()
    {
        // Arrange
        string retryTopicName = Guid.NewGuid().ToString();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var kafkaSettings = BuildKafkaSettings(_statusUpdatedTopicName, retryTopicName);

        var mockEmailService = new Mock<IEmailNotificationService>();
        mockEmailService
            .Setup(x => x.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, Guid.NewGuid().ToString(), SendStatusIdentifierType.OperationId));

        using EmailStatusConsumer emailStatusConsumer = new(producer.Object, kafkaSettings, NullLogger<EmailStatusConsumer>.Instance, mockEmailService.Object);

        EmailSendOperationResult sendOperationResult = new()
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };

        string serializedSendOperationResult = sendOperationResult.Serialize();

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, serializedSendOperationResult);

        await IntegrationTestUtil.EventuallyAsync(
           () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                               i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedRetryTopicName &&
                                               i.Arguments[1] is string message && !string.IsNullOrWhiteSpace(message) && JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options)?.SendResult == serializedSendOperationResult),
           TimeSpan.FromSeconds(15), 
           TimeSpan.FromMilliseconds(1000));
        await emailStatusConsumer.StopAsync(CancellationToken.None);
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
    /// Creates Kafka settings.
    /// </summary>
    /// <returns>
    /// An <see cref="IOptions{KafkaSettings}"/> instance with minimal configuration needed for running the notification consumer tests.
    /// </returns>
    /// <remarks>
    /// Provides a standard configuration with localhost broker address and unit-tests group ID.
    /// </remarks>
    public static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedTopicName, string retryTopicName)
    {
        return Options.Create(new KafkaSettings
        {
            Admin = new AdminSettings { TopicList = [statusUpdatedTopicName, retryTopicName] },
            BrokerAddress = "localhost:9092",
            EmailStatusUpdatedTopicName = statusUpdatedTopicName,
            EmailStatusUpdatedRetryTopicName = retryTopicName,
            Producer = new ProducerSettings(),
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
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
