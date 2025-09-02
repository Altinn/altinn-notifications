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

public class NotificationStatusConsumerBaseTests
{
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
            emailNotificationRepository.Object);

        var emailStatusConsumer = new EmailStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(
            () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                                i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedTopicName &&
                                                i.Arguments[1] is string msg && msg == deliveryReportMessage),
            TimeSpan.FromSeconds(10));

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
            }));

        var smsStatusConsumer = new SmsStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, smsNotificationService);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(() => smsNotificationRepository.Invocations.Any(e => e.Method.Name == nameof(ISmsNotificationRepository.UpdateSendStatus)), TimeSpan.FromSeconds(10));

        await smsStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        logger.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => !string.IsNullOrWhiteSpace(s.ToString())),
                It.Is<SendStatusUpdateException>(ex => ex != null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        producer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))), Times.AtLeastOnce());
        smsNotificationRepository.Verify(e => e.UpdateSendStatus(null, SmsNotificationResultType.Delivered, smsSendOperationResult.GatewayReference), Times.AtLeastOnce());

        var suppressionKey = $"GatewayReference:{smsSendOperationResult.GatewayReference}";
        Assert.True(memoryCache.TryGetValue(suppressionKey, out _), "The suppression key was not added to the memory cache.");
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
            emailNotificationRepository.Object);

        var emailStatusConsumer = new EmailStatusConsumer(producer.Object, memoryCache, kafkaSettings, logger.Object, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        await EventuallyAsync(() => emailNotificationRepository.Invocations.Any(e => e.Method.Name == nameof(IEmailNotificationRepository.UpdateSendStatus)), TimeSpan.FromSeconds(10));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        // Assert
        logger.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => !string.IsNullOrWhiteSpace(s.ToString())),
                It.Is<SendStatusUpdateException>(ex => ex != null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        producer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(e => e.Equals(deliveryReportMessage))), Times.AtLeastOnce());
        emailNotificationRepository.Verify(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId), Times.AtLeastOnce());

        var suppressionKey = $"OperationId:{emailSendOperationResult.OperationId}";
        Assert.True(memoryCache.TryGetValue(suppressionKey, out _), "The suppression key was not added to the memory cache.");
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
            EmailQueueTopicName = "altinn.notifications.email.queue",
            SmsStatusUpdatedTopicName = "altinn.notifications.sms.status.updated",
            EmailStatusUpdatedTopicName = "altinn.notifications.email.status.updated",
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }

    /// <summary>
    /// Repeatedly evaluates a condition until it becomes true or a timeout is reached.
    /// </summary>
    /// <param name="condition">A function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="timeout">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="pollInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    /// <remarks>
    /// This method is useful for testing scenarios where eventual consistency is expected, such as asynchronous operations or distributed systems.
    /// </remarks>
    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(interval);
        }

        throw new XunitException($"Condition not met within timeout ({timeout}).");
    }
}
