using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;
using Xunit.Sdk;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class NotificationStatusConsumerBaseTests : IAsyncLifetime
{
    private const string _emailTopic = "altinn.notifications.email.queue";
    private const string _smsStatusTopic = "altinn.notifications.sms.status.updated";
    private const string _emailStatusTopic = "altinn.notifications.email.status.updated";

    /// <summary>
    /// Called immediately after the class has been created, before it is used.
    /// </summary>
    /// <returns></returns>
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an object is no longer needed.
    /// </summary>
    public async Task DisposeAsync()
    {
        await Dispose(true);
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenUnexpectedException_LogsErrorAndRetries()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReportMessage = emailSendOperationResult.Serialize();

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId))
            .ThrowsAsync(new InvalidOperationException());

        producer
            .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(m => m.Equals(deliveryReportMessage))))
            .ReturnsAsync(true);

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            producer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = kafkaSettings.Value.EmailQueueTopicName
            }),
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig
            {
                EmailPublishBatchSize = 500
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(
            () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                                i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedTopicName &&
                                                i.Arguments[1] is string message && message == deliveryReportMessage),
            TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        emailNotificationRepository.Verify(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId), Times.AtLeastOnce());

        logger.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state != null),
                It.Is<InvalidOperationException>(ex => ex != null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        producer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(m => m.Equals(deliveryReportMessage))), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ProcessSmsDeliveryReport_WhenSendStatusUpdateFails_LogsAndRetriesWithSuppression()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<SmsStatusConsumer>>();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var smsNotificationRepository = new Mock<ISmsNotificationRepository>();

        var smsSendOperationResult = new SmsSendOperationResult
        {
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        var deliveryReportMessage = smsSendOperationResult.Serialize();

        smsNotificationRepository
            .Setup(e => e.UpdateSendStatus(null, SmsNotificationResultType.Delivered, smsSendOperationResult.GatewayReference))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Sms, smsSendOperationResult.GatewayReference, SendStatusIdentifierType.GatewayReference));

        producer
            .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))))
            .ReturnsAsync(true);

        var smsNotificationService = new SmsNotificationService(
            guidService.Object,
            producer.Object,
            dateTimeService.Object,
            smsNotificationRepository.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                SmsQueueTopicName = kafkaSettings.Value.SmsStatusUpdatedTopicName
            }), 
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig
            {
                SmsPublishBatchSize = 500
            }));

        using var smsStatusConsumer = new SmsStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, smsNotificationService);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(
            () => smsNotificationRepository.Invocations.Any(i =>
                i.Method.Name == nameof(ISmsNotificationRepository.UpdateSendStatus) &&
                i.Arguments.Count == 3 &&
                i.Arguments[0] is null &&
                i.Arguments[1] is SmsNotificationResultType deliveryResult && deliveryResult == SmsNotificationResultType.Delivered &&
                i.Arguments[2] is string gatewayReference && gatewayReference == smsSendOperationResult.GatewayReference),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(1000));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        var suppressionKey = $"GatewayReference:{smsSendOperationResult.GatewayReference}";
        Assert.True(memoryCache.TryGetValue(suppressionKey, out _), "The suppression key was not added to the memory cache.");

        logger.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => !string.IsNullOrWhiteSpace(s.ToString())),
                It.Is<SendStatusUpdateException>(ex => ex == null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        producer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))), Times.AtLeastOnce());
        smsNotificationRepository.Verify(e => e.UpdateSendStatus(null, SmsNotificationResultType.Delivered, smsSendOperationResult.GatewayReference), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenSendStatusUpdateFails_LogsAndRetriesWithSuppression()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReportMessage = emailSendOperationResult.Serialize();

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, emailSendOperationResult.OperationId, SendStatusIdentifierType.OperationId));

        producer
            .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))))
            .ReturnsAsync(true);

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            producer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = kafkaSettings.Value.EmailQueueTopicName
            }),
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig
            {
                EmailPublishBatchSize = 500
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(() => emailNotificationRepository.Invocations.Any(e => e.Method.Name == nameof(IEmailNotificationRepository.UpdateSendStatus)), TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        var suppressionKey = $"OperationId:{emailSendOperationResult.OperationId}";
        Assert.True(memoryCache.TryGetValue(suppressionKey, out _), "The suppression key was not added to the memory cache.");

        logger.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => !string.IsNullOrWhiteSpace(s.ToString())),
                It.Is<SendStatusUpdateException>(ex => ex == null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        producer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))), Times.AtLeastOnce());
        emailNotificationRepository.Verify(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId), Times.AtLeastOnce());
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual async Task Dispose(bool disposing)
    {
        await KafkaUtil.DeleteTopicAsync(_emailTopic);
        await KafkaUtil.DeleteTopicAsync(_smsStatusTopic);
        await KafkaUtil.DeleteTopicAsync(_emailStatusTopic);
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
    private static IOptions<KafkaSettings> BuildKafkaSettings()
    {
        return Options.Create(new KafkaSettings
        {
            Admin = new AdminSettings(),
            BrokerAddress = "localhost:9092",
            Producer = new ProducerSettings(),
            EmailQueueTopicName = _emailTopic,
            SmsStatusUpdatedTopicName = _smsStatusTopic,
            EmailStatusUpdatedTopicName = _emailStatusTopic,
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }

    /// <summary>
    /// Repeatedly evaluates a condition until it becomes <c>true</c> or a timeout is reached.
    /// </summary>
    /// <param name="predicate">A function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    private static async Task EventuallyAsync(Func<bool> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        var deadline = DateTime.UtcNow.Add(maximumWaitTime);
        var pollingInterval = checkInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(pollingInterval);
        }

        throw new XunitException($"Condition not met within timeout ({maximumWaitTime}).");
    }
}
