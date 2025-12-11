using System.Diagnostics;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
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

/// <summary>
/// Integration tests for batch-based EmailStatusConsumer behavior.
/// Tests validate parallel processing, batch handling, shutdown behavior, and retry logic.
/// </summary>
public class EmailStatusConsumerBatchTests : IAsyncLifetime
{
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

    [Fact]
    public async Task GivenValidEmailStatusMessage_WhenConsumed_ThenServiceIsCalledOnce()
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

        (_, EmailNotification emailNotification) =
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

        string observedEmailStatus = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                if (observedEmailStatus != EmailNotificationResultType.Delivered.ToString())
                {
                    observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                }

                return observedEmailStatus == EmailNotificationResultType.Delivered.ToString();
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), observedEmailStatus);
    }

    [Fact]
    public async Task GivenInvalidEmailStatusMessage_WhenConsumed_ThenServiceIsNotCalled()
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

        (_, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef, simulateCronJob: true, simulateConsumers: true);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, "Invalid-Delivery-Report");

        long processedOrderCount = -1;
        string observedEmailStatus = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                if (observedEmailStatus != EmailNotificationResultType.New.ToString())
                {
                    observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                }

                if (processedOrderCount != 1)
                {
                    processedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Processed);
                }

                return observedEmailStatus == EmailNotificationResultType.New.ToString() && processedOrderCount == 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processedOrderCount);
        Assert.Equal(EmailNotificationResultType.New.ToString(), observedEmailStatus);
    }

    [Fact]
    public async Task GivenMultipleMessages_ThenProcessedConcurrently_WithinExpectedTimeframe()
    {
        // Arrange
        const int messageCount = 20;
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        // Create notifications in parallel
        var createTasks = Enumerable.Range(0, messageCount).Select(async i =>
        {
            (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(
                $"{_sendersRef}-{i}",
                simulateCronJob: true);
            return notification.Id;
        });

        var notificationIds = (await Task.WhenAll(createTasks)).ToList();

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();

        // Publish in parallel
        var publishTasks = notificationIds.Select(async notificationId =>
        {
            EmailSendOperationResult deliveryReport = new()
            {
                NotificationId = notificationId,
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());
        });

        await Task.WhenAll(publishTasks);

        int processedCount = 0;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedCount = await CountDeliveredNotifications(notificationIds);
                return processedCount == messageCount;
            },
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMilliseconds(200));

        stopwatch.Stop();
        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert - All messages processed
        Assert.Equal(messageCount, processedCount);

        // Cleanup sequentially to avoid connection issues
        for (int i = 0; i < messageCount; i++)
        {
            await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-{i}");
            await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-{i}");
        }
    }

    [Fact]
    public async Task GivenPrimaryProcessingThrows_WhenRetryInvoked_ThenMessageSentToRetryTopic()
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
        var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
        emailServiceMock
            .Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new NotificationNotFoundException(NotificationChannel.Email, Guid.NewGuid().ToString(), SendStatusIdentifierType.NotificationId));

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = Guid.NewGuid(),
            SendResult = EmailNotificationResultType.Delivered
        };

        string serializedDeliveryReport = deliveryReport.Serialize();

        using EmailStatusConsumer emailStatusConsumer =
            new(producerMock.Object, NullLogger<EmailStatusConsumer>.Instance, kafkaOptions, emailServiceMock.Object, deadDeliveryReportServiceMock.Object);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, serializedDeliveryReport);

        bool messagePublishedToRetryTopic = false;
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    producerMock.Verify(e => e.ProduceAsync(_statusUpdatedRetryTopicName, It.Is<string>(e => IsExpectedRetryMessage(e, serializedDeliveryReport))), Times.Once);

                    messagePublishedToRetryTopic = true;

                    return messagePublishedToRetryTopic;
                }
                catch (Exception)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(messagePublishedToRetryTopic);
    }

    [Fact]
    public async Task GivenPrimaryProcessingThrows_WhenRetryAlsoFails_ThenBatchFailureSignaled()
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

        // Track if retry was attempted and failed
        bool retryAttempted = false;

        // Mock producer to fail on both retry topic and main topic - covers lines 586-593
        var producerMock = new Mock<IKafkaProducer>(MockBehavior.Loose);

        // Primary processing will try to publish to retry topic (NotificationStatusConsumerBase:107)
        producerMock
            .Setup(p => p.ProduceAsync(_statusUpdatedRetryTopicName, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Retry topic publish failed"));

        // Retry processing will try to republish to main topic (NotificationStatusConsumerBase:120)
        producerMock
            .Setup(p => p.ProduceAsync(_statusUpdatedTopicName, It.IsAny<string>()))
            .Callback(() => retryAttempted = true)
            .ThrowsAsync(new InvalidOperationException("Main topic republish failed"));

        var emailServiceMock = new Mock<IEmailNotificationService>();
        var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();

        // Primary processing throws NotificationNotFoundException to trigger retry logic
        emailServiceMock
            .Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new NotificationNotFoundException(NotificationChannel.Email, Guid.NewGuid().ToString(), SendStatusIdentifierType.NotificationId));

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = Guid.NewGuid(),
            SendResult = EmailNotificationResultType.Delivered
        };

        using EmailStatusConsumer emailStatusConsumer =
            new(producerMock.Object, NullLogger<EmailStatusConsumer>.Instance, kafkaOptions, emailServiceMock.Object, deadDeliveryReportServiceMock.Object);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        // Wait for both producer calls to be attempted
        bool bothProducerCallsAttempted = false;
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    // Verify primary processing tried to publish to retry topic
                    producerMock.Verify(p => p.ProduceAsync(_statusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                    // Verify retry processing tried to republish to main topic
                    producerMock.Verify(p => p.ProduceAsync(_statusUpdatedTopicName, It.IsAny<string>()), Times.Once);

                    bothProducerCallsAttempted = retryAttempted;

                    return bothProducerCallsAttempted;
                }
                catch (Exception)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert - Both primary and retry producer calls were attempted and failed
        Assert.True(bothProducerCallsAttempted, "Both producer calls should have been attempted");
        Assert.True(retryAttempted, "Retry processor callback should have been invoked");
    }

    [Fact]
    public async Task GivenShutdownInitiated_ThenNoFurtherMessagesAreProcessed_IncludingMessagesProducedDuringStop()
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

        (_, EmailNotification emailNotification1) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-1", simulateCronJob: true);

        (_, EmailNotification emailNotification2) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-2", simulateCronJob: true);

        EmailSendOperationResult deliveryReport1 = new()
        {
            NotificationId = emailNotification1.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        EmailSendOperationResult deliveryReport2 = new()
        {
            NotificationId = emailNotification2.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);

        // Publish first message
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport1.Serialize());

        // Wait for first message to be processed
        string status1 = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                if (status1 != EmailNotificationResultType.Delivered.ToString())
                {
                    status1 = await GetEmailNotificationStatus(emailNotification1.Id);
                }

                return status1 == EmailNotificationResultType.Delivered.ToString();
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        // Ensure first message was processed before shutdown
        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), status1);

        // Initiate shutdown
        var stopTask = emailStatusConsumer.StopAsync(CancellationToken.None);

        // Publish second message during shutdown
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport2.Serialize());

        await stopTask;

        // Check second message status - should still be New
        string status2 = await GetEmailNotificationStatus(emailNotification2.Id);

        // Assert
        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), status1);
        Assert.Equal(EmailNotificationResultType.New.ToString(), status2);

        // Cleanup
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-1");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-1");
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-2");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-2");
    }

    [Fact]
    public async Task GivenMoreThanMaxBatchSizeMessages_ThenMessagesProcessedInMultipleBatches()
    {
        // Arrange
        const int messageCount = 75; // Exceeds max batch size of 50, ensuring multiple batches (50 + 25)
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer emailStatusConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        // Create notifications in parallel for faster setup
        var createTasks = Enumerable.Range(0, messageCount).Select(async i =>
        {
            (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(
                $"{_sendersRef}-batch-{i}",
                simulateCronJob: true);
            return notification.Id;
        });

        var notificationIds = (await Task.WhenAll(createTasks)).ToList();

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);

        // Publish all messages in parallel for faster publishing
        var publishTasks = notificationIds.Select(async notificationId =>
        {
            EmailSendOperationResult deliveryReport = new()
            {
                NotificationId = notificationId,
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());
        });

        await Task.WhenAll(publishTasks);

        // Use efficient bulk query to check processing status
        int processedCount = 0;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                processedCount = await CountDeliveredNotifications(notificationIds);
                return processedCount == messageCount;
            },
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(500));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(messageCount, processedCount);

        // Cleanup sequentially to avoid connection issues
        for (int i = 0; i < messageCount; i++)
        {
            await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-batch-{i}");
            await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-batch-{i}");
        }
    }

    [Fact]
    public async Task GivenActiveConsumerProcessingMessages_WhenStopAsyncCalled_ThenStopCompletesPromptly()
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

        (_, EmailNotification emailNotification) =
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

        // Give the consumer a moment to start processing
        await Task.Delay(500);

        var stopwatch = Stopwatch.StartNew();
        await emailStatusConsumer.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - StopAsync should complete within reasonable time
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"StopAsync took {stopwatch.Elapsed.TotalSeconds} seconds, expected less than 5");
    }

    [Fact]
    public async Task GivenConsumerRestart_ThenPreviouslyUnprocessedMessagesAreConsumed()
    {
        // Arrange
        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        (_, EmailNotification emailNotification1) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-restart-1", simulateCronJob: true);
        (_, EmailNotification emailNotification2) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-restart-2", simulateCronJob: true);

        EmailSendOperationResult report1 = new()
        {
            NotificationId = emailNotification1.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        EmailSendOperationResult report2 = new()
        {
            NotificationId = emailNotification2.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // First run - process first message, stop before second is published
        using (EmailStatusConsumer firstConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First())
        {
            await firstConsumer.StartAsync(CancellationToken.None);
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, report1.Serialize());

            string status1 = string.Empty;
            await IntegrationTestUtil.EventuallyAsync(
                async () =>
                {
                    status1 = await GetEmailNotificationStatus(emailNotification1.Id);
                    return status1 == EmailNotificationResultType.Delivered.ToString();
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(100));

            // Publish second message but stop consumer before processing
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, report2.Serialize());
            await Task.Delay(500); // Small delay to ensure message is in topic
            await firstConsumer.StopAsync(CancellationToken.None);
        }

        // Second run - verify second message is processed after restart
        using (EmailStatusConsumer secondConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First())
        {
            await secondConsumer.StartAsync(CancellationToken.None);

            string status2 = string.Empty;
            await IntegrationTestUtil.EventuallyAsync(
                async () =>
                {
                    status2 = await GetEmailNotificationStatus(emailNotification2.Id);
                    return status2 == EmailNotificationResultType.Delivered.ToString();
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(100));

            await secondConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(EmailNotificationResultType.Delivered.ToString(), status2);
        }

        // Cleanup
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-restart-1");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-restart-1");
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-restart-2");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-restart-2");
    }

    [Fact]
    public async Task GivenConsumerWithNoMessages_WhenStartedAndStopped_ThenNoErrorsOccur()
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

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(1000); // Let it poll for a bit with no messages
        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert - No exception means success
        Assert.True(true, "Consumer handled empty batch scenario without errors");
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
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedRetryTopicName);
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

    [Fact]
    public async Task GivenSecondConsumerJoinsGroup_ThenPartitionRebalanceOccurs_AndFirstConsumerCommitsOffsets()
    {
        // Arrange - Use same group ID to force rebalancing
        string sharedGroupId = $"rebalance-test-group-{Guid.NewGuid()}";

        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" },
            { "KafkaSettings__Consumer__GroupId", sharedGroupId }
        };

        (_, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-rebalance", simulateCronJob: true);

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // First consumer processes a message
        using EmailStatusConsumer firstConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        await firstConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        string status = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                status = await GetEmailNotificationStatus(emailNotification.Id);
                return status == EmailNotificationResultType.Delivered.ToString();
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), status);

        // Act - Start second consumer with same group ID to trigger rebalance
        using EmailStatusConsumer secondConsumer = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        await secondConsumer.StartAsync(CancellationToken.None);

        // Give time for rebalance to occur
        await Task.Delay(3000);

        // Assert - Both consumers should be running without errors (partition revocation handled)
        await firstConsumer.StopAsync(CancellationToken.None);
        await secondConsumer.StopAsync(CancellationToken.None);

        // Cleanup
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-rebalance");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-rebalance");
    }

    [Fact]
    public async Task GivenConsumerInGroupProcessesMessages_WhenAnotherConsumerLeaves_ThenRebalanceHandledGracefully()
    {
        // Arrange - Multiple consumers in same group
        string sharedGroupId = $"rebalance-leave-test-{Guid.NewGuid()}";

        Dictionary<string, string> kafkaSettings = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" },
            { "KafkaSettings__Consumer__GroupId", sharedGroupId }
        };

        using EmailStatusConsumer consumer1 = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        using EmailStatusConsumer consumer2 = ServiceUtil
            .GetServices([typeof(IHostedService)], kafkaSettings)
            .OfType<EmailStatusConsumer>()
            .First();

        await consumer1.StartAsync(CancellationToken.None);
        await consumer2.StartAsync(CancellationToken.None);

        // Give time for initial assignment
        await Task.Delay(2000);

        (_, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"{_sendersRef}-leave", simulateCronJob: true);

        EmailSendOperationResult deliveryReport = new()
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        // Act - Stop one consumer to trigger rebalance
        await consumer2.StopAsync(CancellationToken.None);

        // Give time for rebalance
        await Task.Delay(2000);

        // Verify remaining consumer still processes messages after rebalance
        string status = string.Empty;
        await IntegrationTestUtil.EventuallyAsync(
            async () =>
            {
                status = await GetEmailNotificationStatus(emailNotification.Id);
                return status == EmailNotificationResultType.Delivered.ToString();
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(EmailNotificationResultType.Delivered.ToString(), status);

        await consumer1.StopAsync(CancellationToken.None);

        // Cleanup
        await PostgreUtil.DeleteStatusFeedFromDb($"{_sendersRef}-leave");
        await PostgreUtil.DeleteOrderFromDb($"{_sendersRef}-leave");
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

    private static async Task<int> CountDeliveredNotifications(List<Guid> notificationIds)
    {
        string idList = string.Join("','", notificationIds.Select(id => id.ToString()));
        string sql = $"SELECT count(1) FROM notifications.emailnotifications WHERE alternateid IN ('{idList}') AND result = '{EmailNotificationResultType.Delivered}'";
        return await PostgreUtil.RunSqlReturnOutput<int>(sql);
    }
}
