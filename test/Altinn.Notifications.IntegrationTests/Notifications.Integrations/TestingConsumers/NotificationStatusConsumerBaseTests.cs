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

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class NotificationStatusConsumerBaseTests
{
    [Fact]
    public async Task ProcessStatus_WhenSendStatusUpdateFails_LogsAndRetriesWithSuppression()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var producer = new Mock<IKafkaProducer>(MockBehavior.Strict);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            NotificationId = null,
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
        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
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
            Consumer = new ConsumerSettings { GroupId = "altinn-notifications" },
            EmailQueueTopicName = "altinn.notifications.email.queue",
            SmsStatusUpdatedTopicName = "altinn.notifications.sms.status.updated",
            EmailStatusUpdatedTopicName = "altinn.notifications.email.status.updated"
        });
    }
}
