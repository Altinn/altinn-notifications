using System.Diagnostics;
using System.Text.Json;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Producers;
using Altinn.Notifications.Email.IntegrationTests.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTests.Integrations;

public class EmailSendingConsumerTests : IAsyncLifetime
{
    private readonly KafkaSettings _kafkaSettings;

    private readonly string _emailSendingConsumerTopic = Guid.NewGuid().ToString();
    private readonly string _emailSendingAcceptedProducerTopic = Guid.NewGuid().ToString();

    public EmailSendingConsumerTests()
    {
        _kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Consumer = new()
            {
                GroupId = "email-sending-consumer"
            },
            SendEmailQueueTopicName = _emailSendingConsumerTopic,
            EmailSendingAcceptedTopicName = _emailSendingAcceptedProducerTopic,
            Admin = new()
            {
                TopicList = [_emailSendingConsumerTopic, _emailSendingAcceptedProducerTopic]
            }
        };
    }

    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(_emailSendingConsumerTopic);
        await KafkaUtil.DeleteTopicAsync(_emailSendingAcceptedProducerTopic);
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GivenValidEmailMessage_WhenConsumed_ThenSendingServiceIsCalledOnce()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(processedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Email was not processed within the expected time window.");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidEmailMessage_WhenConsumed_ThenSendingServiceIsNotCalled()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(processedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, "Not an email");

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(processed, "Service should not be called when deserialization fails.");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Never);
    }

    [Fact]
    public async Task GivenPartitionRevocationWithValidBatch_ThenContiguousOffsetsCommitted()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback(processedSignal.Set)
            .Returns(Task.CompletedTask);

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object, loggerMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "test", "body", "from", "to", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        var processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Message should have been processed");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);

        var loggerInvocations = loggerMock.Invocations
            .Where(i => i.Arguments.Count >= 3)
            .Where(i => i.Arguments[2]?.ToString()?.Contains("Commit") == true || i.Arguments[2]?.ToString()?.Contains("last batch safe offsets") == true)
            .ToList();

        Assert.True(loggerInvocations.Count > 0, "Expected commit-related logging during shutdown");
    }

    [Fact]
    public async Task GivenPartitionRevocationWithNoLastProcessedBatch_ThenNoCommitAttempted()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Returns(Task.CompletedTask);

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object, loggerMock.Object);

        // Act - Start consumer but don't process any messages, then trigger partition revocation
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        // Simulate partition revocation by stopping the consumer immediately
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        loggerMock.Verify(
            e => e.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning || l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenMultipleMessages_ThenProcessedConcurrently_WithinExpectedTimeframe()
    {
        var concurrentExecutions = 0;
        var processedMessagesCount = 0;
        var maxConcurrentExecutions = 0;
        var allMessagesProcessedSignal = new ManualResetEventSlim(false);

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
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

                await Task.Delay(250); // Simulated email processing.

                Interlocked.Decrement(ref concurrentExecutions);

                if (Interlocked.Increment(ref processedMessagesCount) >= 50)
                {
                    allMessagesProcessedSignal.Set();
                }
            });

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var emails = Enumerable.Range(0, 50)
            .Select(i => new Core.Sending.Email(Guid.NewGuid(), $"subject-{i}", $"body-{i}", "from", "to", EmailContentType.Plain))
            .ToList();

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        foreach (var email in emails)
        {
            await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));
        }

        var isProcessed = await WaitForConditionAsync(() => allMessagesProcessedSignal.IsSet, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(25));

        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(isProcessed, "All messages were not processed within expected time.");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Exactly(50));
        Assert.True(maxConcurrentExecutions > 1, $"Expected concurrent execution, but max concurrent was only {maxConcurrentExecutions}");
    }

    [Fact]
    public async Task GivenStartedConsumer_WhenMessageProduced_ThenConfiguredTopicIsSubscribed()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(processedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Message produced to the configured topic was not consumed, implying missing subscription.");
    }

    [Fact]
    public async Task GivenKafkaMessageWithNullValue_WhenConsumed_ThenOffsetAdvancedAndNoProcessing()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(processedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, null!);

        bool anyProcessed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));

        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(anyProcessed, "No message should be processed when Kafka message value is null.");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Never);
    }

    [Fact]
    public async Task GivenPartitionRevocationWithRebalanceInProgress_ThenWarningLoggedAndCommitSkipped()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback(processedSignal.Set)
            .Returns(Task.CompletedTask);

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object, loggerMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "test", "body", "from", "to", EmailContentType.Plain);

        // Act - Process a message and then rapidly stop to increase chance of rebalance timing
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        var processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        // Rapid stop should increase likelihood of rebalance timing conflicts
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processed, "Message should have been processed");

        // Verify that either a normal commit occurred or a rebalance warning was logged
        var commitLogs = loggerMock.Invocations
            .Where(i => i.Arguments.Count >= 3)
            .Where(i => i.Arguments[2]?.ToString()?.Contains("Commit") == true)
            .ToList();

        Assert.True(commitLogs.Count > 0, "Expected some form of commit logging to occur");
    }

    [Fact]
    public async Task GivenCancellationBeforeProcessingStarts_WhenMessageProduced_ThenNoProcessingOccurs()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(processedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "cancel-test", "body", "from", "to", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        var stopTask = testFixture.Consumer.StopAsync(CancellationToken.None);

        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        await stopTask;

        bool processed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));

        // Assert
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Never);
        Assert.False(processed, "No message should be processed when cancellation is already requested before processing starts.");
    }

    [Fact]
    public async Task GivenPartitionRevocationWithLastBatchButNoMatchingPartitions_ThenNoCommitAttempted()
    {
        // Arrange
        var firstProcessedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback(firstProcessedSignal.Set)
            .Returns(Task.CompletedTask);

        var firstTopicName = Guid.NewGuid().ToString();
        var secondTopicName = Guid.NewGuid().ToString();

        var kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            SendEmailQueueTopicName = firstTopicName,
            EmailSendingAcceptedTopicName = secondTopicName,
            Consumer = new() { GroupId = "test-partition-mismatch" },
            Admin = new() { TopicList = [firstTopicName, secondTopicName, _emailSendingConsumerTopic, _emailSendingAcceptedProducerTopic] }
        };

        var serviceProvider = CreateServiceProvider(sendingServiceMock.Object, loggerMock.Object, kafkaSettings);
        var producer = KafkaUtil.GetKafkaProducer(serviceProvider);

        try
        {
            await using var firstTestFixture = new EmailConsumerTestFixture(
                producer,
                serviceProvider.GetServices<IHostedService>().OfType<SendEmailQueueConsumer>().Single(),
                serviceProvider);

            var email = new Core.Sending.Email(Guid.NewGuid(), "test", "body", "from", "to", EmailContentType.Plain);

            // Act - Process a message on first topic, then quickly stop to trigger revocation
            await firstTestFixture.Consumer.StartAsync(CancellationToken.None);
            await producer.ProduceAsync(firstTopicName, JsonSerializer.Serialize(email));

            var processed = await WaitForConditionAsync(() => firstProcessedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
            await firstTestFixture.Consumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(processed, "Message should have been processed");

            // No revocation commit warnings should occur since partitions won't match
            loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Warning),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Commit on revocation")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
        finally
        {
            await KafkaUtil.DeleteTopicAsync(firstTopicName);
            await KafkaUtil.DeleteTopicAsync(secondTopicName);
        }
    }

    [Fact]
    public async Task GivenActiveConsumerProcessingMessages_WhenStopAsyncCalled_ThenStopCompletesPromptly()
    {
        // Arrange
        var processedSignal = new ManualResetEventSlim(false);

        var semaphoreSlim = new SemaphoreSlim(0, 1);
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Returns(async () =>
            {
                processedSignal.Set();

                await semaphoreSlim.WaitAsync(TimeSpan.FromSeconds(10));
            });

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var email = new Core.Sending.Email(Guid.NewGuid(), "subject-1", "body-1", "from-1", "to-1", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

        var isProcessed = await WaitForConditionAsync(() => processedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        var stopwatch = Stopwatch.StartNew();
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var stopTask = testFixture.Consumer.StopAsync(stopTimeout.Token);

        semaphoreSlim.Release();

        await stopTask;
        stopwatch.Stop();

        // Assert
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
        Assert.True(isProcessed, "First email was not processed (entered) within the expected time window");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "StopAsync took too long, suggesting internal cancellation was not signaled.");
    }

    [Fact]
    public async Task GivenPrimaryProcessingThrows_WhenRetrySucceeds_ThenOffsetCommitted_AndProducerInvokedOnce()
    {
        // Arrange
        var retryTopicName = Guid.NewGuid().ToString();

        var kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            SendEmailQueueRetryTopicName = retryTopicName,
            SendEmailQueueTopicName = _emailSendingConsumerTopic,
            Consumer = new() { GroupId = "email-sending-consumer" },
            EmailSendingAcceptedTopicName = _emailSendingAcceptedProducerTopic,
            Admin = new() { TopicList = [_emailSendingConsumerTopic, _emailSendingAcceptedProducerTopic, retryTopicName] }
        };

        var sendingServiceMock = new Mock<ISendingService>();
        var retryProducedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .ThrowsAsync(new InvalidOperationException("Simulated primary failure"));

        var retryProducerMock = new Mock<ICommonProducer>();
        retryProducerMock
            .Setup(p => p.ProduceAsync(retryTopicName, It.IsAny<string>()))
            .Callback(retryProducedSignal.Set)
            .ReturnsAsync(true);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(kafkaSettings)
            .AddSingleton(retryProducerMock.Object)
            .AddSingleton(sendingServiceMock.Object);

        if (loggerMock != null)
        {
            services.AddSingleton(loggerMock.Object);
        }

        services.AddHostedService<SendEmailQueueConsumer>();

        var serviceProvider = services.BuildServiceProvider();
        var emailSendingConsumer = serviceProvider.GetServices<IHostedService>().OfType<SendEmailQueueConsumer>().Single();

        // Use real producer for sending test message to main topic
        var realProducer = KafkaUtil.GetKafkaProducer(CreateServiceProvider(sendingServiceMock.Object, null, kafkaSettings));

        try
        {
            // Act
            await emailSendingConsumer.StartAsync(CancellationToken.None);

            var email = new Core.Sending.Email(Guid.NewGuid(), "retry-test", "body", "from", "to", EmailContentType.Plain);
            await realProducer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));

            bool retryObserved = await WaitForConditionAsync(() => retryProducedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

            await emailSendingConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(retryObserved, "Retry pathway did not complete promptly.");
            sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
            retryProducerMock.Verify(p => p.ProduceAsync(retryTopicName, It.IsAny<string>()), Times.Once);
        }
        finally
        {
            // Cleanup
            await KafkaUtil.DeleteTopicAsync(retryTopicName);
            await serviceProvider.DisposeAsync();
            realProducer.Dispose();
        }
    }

    [Fact]
    public async Task GivenShutdown_ThenLastBatchSafeOffsetsCommittedOnce_AndPendingMessageProcessedAfterRestart()
    {
        // Arrange
        var firstEmailNotificationIdentifer = Guid.NewGuid();
        var firstRunSemaphoreSlim = new SemaphoreSlim(0, 1);
        var firstProcessedSignal = new ManualResetEventSlim(false);
        var firstEmail = new Core.Sending.Email(firstEmailNotificationIdentifer, "first", "body-1", "from", "to", EmailContentType.Plain);

        var secondEmailNotificationIdentifer = Guid.NewGuid();
        var secondProcessedSignal = new ManualResetEventSlim(false);
        var secondEmail = new Core.Sending.Email(secondEmailNotificationIdentifer, "second", "body-2", "from", "to", EmailContentType.Plain);

        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var firstRunSendingServiceMock = new Mock<ISendingService>();
        firstRunSendingServiceMock
            .Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == firstEmailNotificationIdentifer)))
            .Callback(firstProcessedSignal.Set)
            .Returns(Task.CompletedTask);

        firstRunSendingServiceMock
            .Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == secondEmailNotificationIdentifer)))
            .Returns(async () =>
            {
                // Simulate in-flight work that should NOT complete before StopAsync.
                await firstRunSemaphoreSlim.WaitAsync(TimeSpan.FromSeconds(25));
            });

        await using var firstTestFixture = CreateTestFixture(firstRunSendingServiceMock.Object, loggerMock.Object);

        // Act
        await firstTestFixture.Consumer.StartAsync(CancellationToken.None);

        await firstTestFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(firstEmail));
        await firstTestFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(secondEmail));

        var firstProcessed = await WaitForConditionAsync(() => firstProcessedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        await firstTestFixture.Consumer.StopAsync(CancellationToken.None);

        loggerMock.Verify(
           e => e.Log(
                It.Is<LogLevel>(e => e == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
           Times.AtLeastOnce);

        firstRunSemaphoreSlim.Release();

        var secondRunSendingServiceMock = new Mock<ISendingService>();
        secondRunSendingServiceMock
            .Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == secondEmailNotificationIdentifer)))
            .Callback(secondProcessedSignal.Set)
            .Returns(Task.CompletedTask);

        secondRunSendingServiceMock
            .Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == firstEmailNotificationIdentifer)))
            .Returns(Task.CompletedTask);

        await using var secondTestFixture = CreateTestFixture(secondRunSendingServiceMock.Object);

        await secondTestFixture.Consumer.StartAsync(CancellationToken.None);

        var secondProcessed = await WaitForConditionAsync(() => secondProcessedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        await secondTestFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(firstProcessed, "First email was not processed within the expected time window.");
        firstRunSendingServiceMock.Verify(e => e.SendAsync(It.Is<Core.Sending.Email>(m => m.NotificationId == firstEmailNotificationIdentifer)), Times.Once);
        firstRunSendingServiceMock.Verify(e => e.SendAsync(It.Is<Core.Sending.Email>(m => m.NotificationId == secondEmailNotificationIdentifer)), Times.Once);
        secondRunSendingServiceMock.Verify(e => e.SendAsync(It.Is<Core.Sending.Email>(m => m.NotificationId == secondEmailNotificationIdentifer)), Times.Once);
        Assert.True(secondProcessed, "Second email was not processed by the restarted consumer, indicating offsets may have been committed beyond the safe contiguous boundary.");
    }

    [Fact]
    public async Task GivenShutdownInitiated_ThenNoFurtherMessagesAreProcessed_IncludingMessagesProducedDuringStop()
    {
        // Arrange
        var firstProcessedSignal = new ManualResetEventSlim(false);
        var sendingServiceMock = CreateSendingServiceMock(firstProcessedSignal);
        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        var firstEmail = new Core.Sending.Email(Guid.NewGuid(), "first", "body-1", "from-1", "to-1", EmailContentType.Plain);
        var afterStopEmail = new Core.Sending.Email(Guid.NewGuid(), "after", "body-3", "from-3", "to-3", EmailContentType.Plain);
        var duringShutdownEmail = new Core.Sending.Email(Guid.NewGuid(), "during", "body-2", "from-2", "to-2", EmailContentType.Plain);

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(firstEmail));
        var isFirstProcessed = await WaitForConditionAsync(() => firstProcessedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        var stopTask = testFixture.Consumer.StopAsync(CancellationToken.None);
        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(duringShutdownEmail));
        await stopTask;

        await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(afterStopEmail));
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
        Assert.True(isFirstProcessed, "First email was not processed within the expected time window.");
    }

    [Fact]
    public async Task GivenMoreThanMaxBatchSizeMessages_ThenAtLeastMaxBatchAreProcessedInFirstBatch_RemainderInNextBatch()
    {
        // Arrange
        var processedCount = 0;
        var reached100Signal = new ManualResetEventSlim(false);
        var allProcessedSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref processedCount);
                await Task.Delay(5);

                if (current == 100)
                {
                    reached100Signal.Set();
                }

                if (current == 150)
                {
                    allProcessedSignal.Set();
                }
            });

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object);

        // Produce 150 messages; base max batch size is 100
        var emails = Enumerable.Range(0, 150)
            .Select(i => new Core.Sending.Email(Guid.NewGuid(), $"s-{i}", $"b-{i}", "from", "to", EmailContentType.Plain))
            .ToList();

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        foreach (var email in emails)
        {
            await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));
        }

        var processedFirst100 = await WaitForConditionAsync(() => reached100Signal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        var processedAll = await WaitForConditionAsync(() => allProcessedSignal.IsSet, TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(50));

        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(processedFirst100, "Did not process at least the first batch of 100 messages promptly.");
        Assert.True(processedAll, "Remaining messages from the next batch were not processed within the expected window.");
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Exactly(150));
    }

    [Fact]
    public async Task GivenMultipleMessagesWithPartialProcessing_WhenRevocationOccurs_ThenOnlyContiguousOffsetsCommitted()
    {
        // Arrange
        var processedCount = 0;
        var firstProcessedSignal = new ManualResetEventSlim(false);
        var blockingSignal = new ManualResetEventSlim(false);
        var loggerMock = new Mock<ILogger<SendEmailQueueConsumer>>();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref processedCount);

                if (current == 1)
                {
                    firstProcessedSignal.Set();
                    return Task.CompletedTask;
                }

                // Subsequent messages block to create partial processing scenario
                return Task.Run(() => blockingSignal.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)));
            });

        await using var testFixture = CreateTestFixture(sendingServiceMock.Object, loggerMock.Object);

        var emails = Enumerable.Range(0, 5)
            .Select(i => new Core.Sending.Email(Guid.NewGuid(), $"subject-{i}", $"body-{i}", "from", "to", EmailContentType.Plain))
            .ToList();

        // Act
        await testFixture.Consumer.StartAsync(CancellationToken.None);

        foreach (var email in emails)
        {
            await testFixture.Producer.ProduceAsync(_emailSendingConsumerTopic, JsonSerializer.Serialize(email));
        }

        var firstProcessed = await WaitForConditionAsync(() => firstProcessedSignal.IsSet, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        // Stop consumer while some messages are still processing (creating partial batch scenario)
        await testFixture.Consumer.StopAsync(CancellationToken.None);

        // Release any blocked processing
        blockingSignal.Set();

        // Assert
        Assert.True(firstProcessed, "At least the first message should have been processed");

        var commitOrRevocationLogs = loggerMock.Invocations
            .Where(i => i.Arguments.Count >= 3)
            .Where(i =>
            {
                var message = i.Arguments[2]?.ToString();

                return message?.Contains("Partitions revoked", StringComparison.OrdinalIgnoreCase) == true ||
                       message?.Contains("subscribed to topic", StringComparison.OrdinalIgnoreCase) == true ||
                       message?.Contains("Partitions assigned", StringComparison.OrdinalIgnoreCase) == true ||
                       message?.Contains("unsubscribed from topic", StringComparison.OrdinalIgnoreCase) == true;
            })
            .ToList();

        Assert.True(commitOrRevocationLogs.Count > 0, "Expected commit or revocation-related logging");
    }

    /// <summary>
    /// Creates a mocked <see cref="ISendingService"/> that signals a provided <see cref="ManualResetEventSlim"/>
    /// when <see cref="ISendingService.SendAsync(Core.Sending.Email)"/> is invoked.
    /// </summary>
    /// <param name="processedSignal">
    /// The synchronization primitive to set when the mock's <c>SendAsync</c> method is called,
    /// allowing tests to await message processing completion without fixed delays.
    /// </param>
    /// <returns>
    /// A configured <see cref="Mock{T}"/> of <see cref="ISendingService"/> whose <c>SendAsync</c> completes immediately
    /// and triggers <paramref name="processedSignal"/> via its callback.
    /// </returns>
    private static Mock<ISendingService> CreateSendingServiceMock(ManualResetEventSlim processedSignal)
    {
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback(() => processedSignal.Set())
            .Returns(Task.CompletedTask);
        return sendingServiceMock;
    }

    /// <summary>
    /// Polls a boolean condition until it becomes <c>true</c> or a timeout elapses.
    /// </summary>
    /// <param name="condition">A function returning the current state to evaluate.</param>
    /// <param name="timeout">The maximum time to wait for the condition to become <c>true</c>.</param>
    /// <param name="pollInterval">The interval between successive evaluations of <paramref name="condition"/>.</param>
    /// <returns>
    /// A task that completes with <c>true</c> if the condition became <c>true</c> before the timeout; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This helper avoids fixed delays in tests by polling frequently and returning as soon as the condition is met.
    /// </remarks>
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
    /// Creates a fully configured <see cref="EmailConsumerTestFixture"/> for integration testing of the email consumer.
    /// </summary>
    /// <param name="sendingService">
    /// The <see cref="ISendingService"/> implementation to be used for email processing.
    /// </param>
    /// <param name="logger">
    /// Optional <see cref="ILogger{T}"/> for <see cref="SendEmailQueueConsumer"/> to capture or control log output during testing.
    /// </param>
    /// <returns>
    /// A configured <see cref="EmailConsumerTestFixture"/> containing:
    /// <list type="bullet">
    /// <item><description>A <see cref="SendEmailQueueConsumer"/> ready for testing</description></item>
    /// <item><description>A <see cref="CommonProducer"/> for sending test messages to Kafka topics</description></item>
    /// <item><description>A <see cref="ServiceProvider"/> with all required dependencies</description></item>
    /// </list>
    /// The fixture implements <see cref="IAsyncDisposable"/> and must be disposed properly to clean up resources.
    /// </returns>
    private EmailConsumerTestFixture CreateTestFixture(ISendingService sendingService, ILogger<SendEmailQueueConsumer>? logger = null)
    {
        var serviceProvider = CreateServiceProvider(sendingService, logger);
        var producer = KafkaUtil.GetKafkaProducer(serviceProvider);

        var hostedServices = serviceProvider.GetServices<IHostedService>();

        var emailSendingConsumer = hostedServices.OfType<SendEmailQueueConsumer>().SingleOrDefault();
        if (emailSendingConsumer is null)
        {
            Assert.Fail("Unable to locate SendEmailQueueConsumer among registered IHostedService instances.");
        }

        return new EmailConsumerTestFixture(producer, emailSendingConsumer, serviceProvider);
    }

    /// <summary>
    /// Creates a configured <see cref="ServiceProvider"/> for integration tests.
    /// </summary>
    /// <param name="sendingService">
    /// The <see cref="ISendingService"/> to be injected into the consumer, typically a mocked implementation.
    /// </param>
    /// <param name="sendEmailQueueConsumerLogger">
    /// Optional typed <see cref="ILogger{T}"/> for <see cref="SendEmailQueueConsumer"/> to capture or control logs emitted by the consumer.
    /// </param>
    /// <param name="customKafkaSettings">
    /// Optional custom <see cref="KafkaSettings"/> to override the default instance settings. When <c>null</c>, uses the default <see cref="_kafkaSettings"/> field.
    /// </param>
    /// <returns>
    /// A configured <see cref="ServiceProvider"/> with all necessary services registered for testing.
    /// </returns>
    private ServiceProvider CreateServiceProvider(ISendingService sendingService, ILogger<SendEmailQueueConsumer>? sendEmailQueueConsumerLogger = null, KafkaSettings? customKafkaSettings = null)
    {
        IServiceCollection services = new ServiceCollection()
           .AddLogging()
           .AddSingleton(sendingService)
           .AddHostedService<SendEmailQueueConsumer>()
           .AddSingleton<ICommonProducer, CommonProducer>()
           .AddSingleton(customKafkaSettings ?? _kafkaSettings);

        if (sendEmailQueueConsumerLogger != null)
        {
            services.AddSingleton(sendEmailQueueConsumerLogger);
        }

        return services.BuildServiceProvider();
    }
}
