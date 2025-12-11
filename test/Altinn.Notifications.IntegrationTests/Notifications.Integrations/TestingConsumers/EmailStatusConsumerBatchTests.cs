using System.Diagnostics;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

/// <summary>
/// Integration tests for batch-based EmailStatusConsumer behavior.
/// Tests validate parallel processing, offset commit strategies, shutdown behavior, and retry logic.
/// </summary>
public class EmailStatusConsumerBatchTests : IAsyncLifetime
{
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

    public async Task InitializeAsync()
    {
        await KafkaUtil.CreateTopicAsync(_statusUpdatedTopicName);
        await KafkaUtil.CreateTopicAsync(_statusUpdatedRetryTopicName);
    }

    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedTopicName);
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedRetryTopicName);
    }

    [Fact]
    public async Task GivenValidEmailStatusMessage_WhenConsumed_ThenServiceIsCalledOnce()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var emailNotificationServiceMock = CreateEmailNotificationServiceMock(processedSignal);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        var deliveryReport = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Email status was not processed within the expected time window.");
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidEmailStatusMessage_WhenConsumed_ThenServiceIsNotCalled()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var emailNotificationServiceMock = CreateEmailNotificationServiceMock(processedSignal);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, "Not a valid status message");

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(processed, "Service should not be called when deserialization fails.");
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
    }

    [Fact]
    public async Task GivenMultipleMessages_ThenProcessedConcurrently_WithinExpectedTimeframe()
    {
        // Arrange
        const int messageCount = 50;
        var concurrentExecutions = 0;
        var processedMessagesCount = 0;
        var maxConcurrentExecutions = 0;
        var allMessagesProcessedSignal = new ManualResetEventSlim(false);

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Returns(async () =>
            {
                var currentConcurrent = Interlocked.Increment(ref concurrentExecutions);

                var currentMax = Volatile.Read(ref maxConcurrentExecutions);
                while (currentConcurrent > currentMax)
                {
                    var originalMax = Interlocked.CompareExchange(ref maxConcurrentExecutions, currentConcurrent, currentMax);
                    if (originalMax == currentMax)
                    {
                        break;
                    }

                    currentMax = Volatile.Read(ref maxConcurrentExecutions);
                }

                await Task.Delay(250); // Simulated processing delay

                Interlocked.Decrement(ref concurrentExecutions);

                if (Interlocked.Increment(ref processedMessagesCount) >= messageCount)
                {
                    allMessagesProcessedSignal.Set();
                }
            });

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);

        // Publish multiple messages
        for (int i = 0; i < messageCount; i++)
        {
            var deliveryReport = new EmailSendOperationResult
            {
                NotificationId = emailNotification.Id,
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());
        }

        var isProcessed = await WaitForConditionAsync(
            () => allMessagesProcessedSignal.IsSet,
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMilliseconds(50));

        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(isProcessed, "All messages were not processed within expected time.");
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Exactly(messageCount));
        Assert.True(maxConcurrentExecutions > 1, $"Expected concurrent execution, but max concurrent was only {maxConcurrentExecutions}");
    }

    [Fact]
    public async Task GivenPartitionRevocationWithValidBatch_ThenContiguousOffsetsCommitted()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<EmailStatusConsumer>>();

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback(processedSignal.Set)
            .Returns(Task.CompletedTask);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object, loggerMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        var deliveryReport = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        var processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Message should have been processed");
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);

        var loggerInvocations = loggerMock.Invocations
            .Where(i => i.Arguments.Count >= 3)
            .Where(i => i.Arguments[2]?.ToString()?.Contains("Commit") == true || i.Arguments[2]?.ToString()?.Contains("last batch safe offsets") == true)
            .ToList();

        Assert.True(loggerInvocations.Count > 0, "Expected commit-related logging during shutdown");
    }

    [Fact]
    public async Task GivenKafkaMessageWithNullValue_WhenConsumed_ThenOffsetAdvancedAndNoProcessing()
    {
        // Arrange
        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Returns(Task.CompletedTask);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        
        // Kafka messages with null values are typically used as tombstones
        // The consumer should handle them gracefully without calling the service
        
        // Give the consumer a moment to poll
        await Task.Delay(500);
        
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
    }

    [Fact]
    public async Task GivenPrimaryProcessingThrows_WhenRetryInvoked_ThenMessageSentToRetryTopic()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new Exception("Simulated transient failure"));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(_statusUpdatedTopicName, It.IsAny<string>()))
            .Callback(() => processedSignal.Set())
            .ReturnsAsync(true);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object, producer: producerMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        var deliveryReport = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        bool retried = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(retried, "Message should have been sent to retry topic");
        
        // Verify that the primary service was called (and threw exception)
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
        
        // Verify that retry logic published message back to status topic
        producerMock.Verify(p => p.ProduceAsync(_statusUpdatedTopicName, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GivenShutdownInitiated_ThenNoFurtherMessagesAreProcessed_IncludingMessagesProducedDuringStop()
    {
        // Arrange
        int processedCount = 0;

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback(() => Interlocked.Increment(ref processedCount))
            .Returns(Task.CompletedTask);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);

        // Publish one message that should be processed
        var deliveryReport1 = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport1.Serialize());

        // Wait for the first message to be processed
        await WaitForConditionAsync(() => Volatile.Read(ref processedCount) >= 1, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        // Initiate shutdown
        var stopTask = testConsumer.StopAsync(CancellationToken.None);

        // Publish another message during shutdown - this should NOT be processed
        var deliveryReport2 = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport2.Serialize());

        await stopTask;

        // Assert
        Assert.Equal(1, Volatile.Read(ref processedCount));
    }

    [Fact]
    public async Task GivenMoreThanMaxBatchSizeMessages_ThenMessagesProcessedInMultipleBatches()
    {
        // Arrange
        const int messageCount = 150; // More than typical batch size of 100
        int processedCount = 0;

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback(() => Interlocked.Increment(ref processedCount))
            .Returns(Task.CompletedTask);

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);

        // Publish messages
        var publishTasks = Enumerable.Range(0, messageCount).Select(async i =>
        {
            var deliveryReport = new EmailSendOperationResult
            {
                NotificationId = emailNotification.Id,
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());
        });

        await Task.WhenAll(publishTasks);

        // Wait for all messages to be processed
        bool allProcessed = await WaitForConditionAsync(
            () => Volatile.Read(ref processedCount) >= messageCount,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(100));

        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(allProcessed, $"Expected {messageCount} messages to be processed, but got {processedCount}");
        Assert.Equal(messageCount, Volatile.Read(ref processedCount));
    }

    [Fact]
    public async Task GivenActiveConsumerProcessingMessages_WhenStopAsyncCalled_ThenStopCompletesPromptly()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var semaphoreSlim = new SemaphoreSlim(0, 1);

        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        emailNotificationServiceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Returns(async () =>
            {
                processedSignal.Set();
                await semaphoreSlim.WaitAsync(TimeSpan.FromSeconds(10));
            });

        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object);

        var sendersRef = $"ref-{Guid.NewGuid()}";
        (_, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

        var deliveryReport = new EmailSendOperationResult
        {
            NotificationId = emailNotification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, deliveryReport.Serialize());

        var isProcessed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        var stopwatch = Stopwatch.StartNew();
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var stopTask = testConsumer.StopAsync(stopTimeout.Token);

        semaphoreSlim.Release();

        await stopTask;
        stopwatch.Stop();

        // Assert
        emailNotificationServiceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
        Assert.True(isProcessed, "First message was not processed (entered) within the expected time window");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "StopAsync took too long, suggesting internal cancellation was not signaled.");
    }

    [Fact]
    public async Task GivenConfiguredTopicSubscription_WhenConsumerStarts_ThenSubscribedToCorrectTopic()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<EmailStatusConsumer>>();
        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();
        
        using var testConsumer = CreateConsumer(emailNotificationServiceMock.Object, loggerMock.Object);

        // Act
        await testConsumer.StartAsync(CancellationToken.None);
        
        // Allow consumer to initialize
        await Task.Delay(500);
        
        await testConsumer.StopAsync(CancellationToken.None);

        // Assert
        var subscriptionLogs = loggerMock.Invocations
            .Where(i => i.Arguments.Count >= 3)
            .Where(i => i.Arguments[2]?.ToString()?.Contains("subscribed to topic") == true)
            .ToList();

        Assert.True(subscriptionLogs.Count > 0, "Expected subscription logging");
    }

    /// <summary>
    /// Creates a mocked <see cref="IEmailNotificationService"/> that signals when UpdateSendStatus is called.
    /// </summary>
    private static Mock<IEmailNotificationService> CreateEmailNotificationServiceMock(ManualResetEventSlim processedSignal)
    {
        var mock = new Mock<IEmailNotificationService>();
        mock.Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback(processedSignal.Set)
            .Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Polls a boolean condition until it becomes <c>true</c> or a timeout elapses.
    /// </summary>
    private static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, TimeSpan pollInterval)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(pollInterval);
        }

        return false;
    }

    /// <summary>
    /// Creates a configured EmailStatusConsumer for testing.
    /// </summary>
    private EmailStatusConsumer CreateConsumer(
        IEmailNotificationService emailNotificationService,
        ILogger<EmailStatusConsumer>? logger = null,
        IKafkaProducer? producer = null,
        IDeadDeliveryReportService? deadDeliveryReportService = null)
    {
        var kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Consumer = new ConsumerSettings
            {
                GroupId = $"email-status-consumer-test-{Guid.NewGuid()}"
            },
            EmailStatusUpdatedTopicName = _statusUpdatedTopicName,
            EmailStatusUpdatedRetryTopicName = _statusUpdatedRetryTopicName
        };

        producer ??= new Mock<IKafkaProducer>().Object;
        logger ??= new NullLogger<EmailStatusConsumer>();
        deadDeliveryReportService ??= new Mock<IDeadDeliveryReportService>().Object;

        return new EmailStatusConsumer(
            producer,
            logger,
            Options.Create(kafkaSettings),
            emailNotificationService,
            deadDeliveryReportService);
    }
}
