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
    private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

    [Fact]
    public async Task ConsumeInvalidMessage_ShouldNotUpdateStatus()
    {
        // Arrange
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        (NotificationOrder notificationOrder, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true, simulateConsumers: true);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, string.Empty);

        // Wait until order is processed, capture status once when it happens
        long processedOrderCount = -1;
        string? observedSmsStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedOrderCount = await CountOrdersWithStatus(notificationOrder.Id, OrderProcessingStatus.Processed);

                if (processedOrderCount == 1)
                {
                    observedSmsStatus = await GetEmailNotificationStatus(emailNotification.Id);
                    return true;
                }

                return false;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processedOrderCount);
        Assert.Equal(SmsNotificationResultType.New.ToString(), observedSmsStatus);
    }

    [Fact]
    public async Task ConsumeDeliveredStatus_ShouldMarkOrderCompleted_WithStatusFeedEntry()
    {
        // Arrange
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        (NotificationOrder notificationOrder, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        string? observedEmailStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                return string.Equals(observedEmailStatus, EmailNotificationResultType.Delivered.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        long completedOrderCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                completedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Completed);
                return completedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        int statusFeedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(notificationOrder.Id);
                return statusFeedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, completedOrderCount);
        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), observedEmailStatus);
    }

    [Fact]
    public async Task ConsumeSucceededStatus_ShouldMarkOrderProcessed_WithoutStatusFeedEntry()
    {
        // Arrange
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        (NotificationOrder notificationOrder, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        string? observedEmailStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                return string.Equals(observedEmailStatus, EmailNotificationResultType.Succeeded.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        long processedOrderCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Processed);
                return processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        int statusFeedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(notificationOrder.Id);
                return statusFeedCount == 0;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert using captured values
        Assert.Equal(0, statusFeedCount);
        Assert.Equal(1, processedOrderCount);
        Assert.Equal(EmailNotificationResultType.Succeeded.ToString(), observedEmailStatus);
    }

    [Theory]
    [InlineData(SendStatusIdentifierType.OperationId)]
    [InlineData(SendStatusIdentifierType.NotificationId)]
    public async Task ConsumeDeliveredStatus_ServiceThrows_ShouldPublishRetryMessage(SendStatusIdentifierType identifierType)
    {
        // Arrange
        var kafkaOptions = Options.Create(new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Producer = new ProducerSettings(),
            EmailStatusUpdatedTopicName = _statusUpdatedTopicName,
            EmailStatusUpdatedRetryTopicName = _statusUpdatedRetryTopicName,
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });

        var producerMock = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailServiceMock = new Mock<IEmailNotificationService>();
        emailServiceMock
            .Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, Guid.NewGuid().ToString(), identifierType));

        EmailSendOperationResult deliveryReport = identifierType == SendStatusIdentifierType.NotificationId
            ? new EmailSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = EmailNotificationResultType.Delivered }
            : new EmailSendOperationResult { OperationId = Guid.NewGuid().ToString(), SendResult = EmailNotificationResultType.Delivered };

        string serializedDeliveryReport = deliveryReport.Serialize();

        using EmailStatusConsumer consumer =
            new(producerMock.Object, NullLogger<EmailStatusConsumer>.Instance, kafkaOptions, emailServiceMock.Object);

        // Act
        await consumer.StartAsync(CancellationToken.None);

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, serializedDeliveryReport);

        await IntegrationTestUtil.EventuallyAsync(
            () => producerMock.Invocations.Any(inv =>
                inv.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                inv.Arguments[0] is string topic && topic == _statusUpdatedRetryTopicName &&
                inv.Arguments[1] is string updateStatusRetryMessage &&
                JsonSerializer.Deserialize<UpdateStatusRetryMessage>(updateStatusRetryMessage, JsonSerializerOptionsProvider.Options)?.SendOperationResult == serializedDeliveryReport),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await consumer.StopAsync(CancellationToken.None);

        // Assert
        producerMock.Verify(
          e => e.ProduceAsync(_statusUpdatedRetryTopicName, It.Is<string>(e => IsExpectedRetryMessage(e, serializedDeliveryReport))),
          Times.Once,
          $"Expected exactly one retry message on topic '{_statusUpdatedRetryTopicName}' containing the original serialized SendOperationResult.");
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
    public async Task ConsumeFailedStatus_ShouldMarkOrderCompleted_WithStatusFeedEntry(EmailNotificationResultType resultType)
    {
        // Arrange
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        (_, EmailNotification notification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true);

        EmailSendOperationResult deliveryReport = new()
        {
            SendResult = resultType,
            NotificationId = notification.Id,
            OperationId = Guid.NewGuid().ToString()
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        string? observedEmailStatus = null;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                observedEmailStatus = await GetEmailNotificationStatus(notification.Id);
                return string.Equals(observedEmailStatus, resultType.ToString(), StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        long completedCount = -1;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                completedCount = await CountOrdersWithStatus(notification.Id, OrderProcessingStatus.Completed);
                return completedCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        Assert.Equal(1, completedCount);
        Assert.Equal(resultType.ToString(), observedEmailStatus);
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
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedTopicName);
    }

    private static bool IsExpectedRetryMessage(string message, string expectedSendOperationResult)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        try
        {
            var retry = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options);
            return retry?.SendOperationResult == expectedSendOperationResult;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> GetEmailNotificationStatus(Guid emailNotificationAlternateid)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{emailNotificationAlternateid}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task<long> CountOrdersWithStatus(Guid orderAlternateid, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.emailnotifications e on e._orderid = o._id where e.alternateid = '{orderAlternateid}' and o.processedstatus = '{orderProcessingStatus}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
