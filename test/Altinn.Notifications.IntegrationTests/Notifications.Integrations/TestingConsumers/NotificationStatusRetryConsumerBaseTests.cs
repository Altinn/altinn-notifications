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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class NotificationStatusRetryConsumerBaseTests : IAsyncLifetime
{
    private readonly string _emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();
    private IOptions<KafkaSettings> _kafkaSettings = Options.Create(new KafkaSettings());

    [Fact]
    public async Task ConsumeRetryMessage_WhenDeserializationReturnsNull_NoProcessingOccurs()
    {
        // Arrange
        bool deserializationErrorLogged = false;
        var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationService = new Mock<IEmailNotificationService>();
        var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

        logger
            .Setup(e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Message deserialization failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => deserializationErrorLogged = true);

        using var emailStatusConsumer = new EmailStatusRetryConsumer(
            kafkaProducer.Object,
            logger.Object,
            _kafkaSettings,
            emailNotificationService.Object,
            deadDeliveryReportService.Object);

        await emailStatusConsumer.StartAsync(CancellationToken.None);

        // Act
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, "null");

        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                return deserializationErrorLogged;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        // Assert
        emailNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
        deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

        await emailStatusConsumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ConsumeRetryMessage_WhenExceededRetryThreshold_InsertDeadDeliveryReport()
    {
        // Arrange
        var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationService = new Mock<IEmailNotificationService>();
        var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = emailSendOperationResult.Serialize();

        var updateStatusRetryMessage = new UpdateStatusRetryMessage
        {
            Attempts = 50,
            SendOperationResult = deliveryReport,
            FirstSeen = DateTime.UtcNow.AddSeconds(-305),
            LastAttempt = DateTime.UtcNow.AddSeconds(-5)
        };

        deadDeliveryReportService
            .Setup(e => e.InsertAsync(It.Is<DeadDeliveryReport>(e => e.DeliveryReport == deliveryReport), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        using var emailStatusConsumer = new EmailStatusRetryConsumer(
            kafkaProducer.Object,
            logger.Object,
            _kafkaSettings,
            emailNotificationService.Object,
            deadDeliveryReportService.Object);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, updateStatusRetryMessage.Serialize());

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    deadDeliveryReportService.Verify(
                        e => e.InsertAsync(
                        It.Is<DeadDeliveryReport>(e =>
                            e.DeliveryReport == deliveryReport &&
                            e.FirstSeen == updateStatusRetryMessage.FirstSeen &&
                            e.AttemptCount == updateStatusRetryMessage.Attempts &&
                            e.LastAttempt == updateStatusRetryMessage.LastAttempt &&
                            e.Channel == DeliveryReportChannel.AzureCommunicationServices),
                        It.IsAny<CancellationToken>()),
                        Times.Once);

                    kafkaProducer.Verify(e => e.ProduceAsync(It.Is<string>(e => e == _kafkaSettings.Value.EmailStatusUpdatedRetryTopicName), It.IsAny<string>()), Times.Never);

                    emailNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ConsumeRetryMessage_WhenWithinRetryThresholdAndUpdateFails_MessageRepublishedWithIncrementedAttempts()
    {
        // Arrange
        var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Strict);
        var emailNotificationService = new Mock<IEmailNotificationService>();
        var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };
        var deliveryReport = emailSendOperationResult.Serialize();

        var originalAttempts = 3;
        var originalLastAttempt = DateTime.UtcNow.AddSeconds(-5);

        var updateStatusRetryMessage = new UpdateStatusRetryMessage
        {
            Attempts = originalAttempts,
            SendOperationResult = deliveryReport,
            LastAttempt = originalLastAttempt,
            FirstSeen = DateTime.UtcNow.AddSeconds(-30)
        };

        emailNotificationService
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new Exception("Simulated failure"));

        string? republishedStatusMessage = null;

        kafkaProducer
            .Setup(p => p.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedStatusMessage = message)
            .ReturnsAsync(true);

        using var emailStatusConsumer = new EmailStatusRetryConsumer(
            kafkaProducer.Object,
            logger.Object,
            _kafkaSettings,
            emailNotificationService.Object,
            deadDeliveryReportService.Object);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, updateStatusRetryMessage.Serialize());

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    emailNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
                    kafkaProducer.Verify(p => p.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);

        Assert.NotNull(republishedStatusMessage);
        var republishedMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(republishedStatusMessage, JsonSerializerOptionsProvider.Options);

        Assert.NotNull(republishedMessage);
        Assert.Equal(originalAttempts + 1, republishedMessage.Attempts);
        Assert.True(republishedMessage.LastAttempt > originalLastAttempt);
        Assert.Equal(deliveryReport, republishedMessage.SendOperationResult);
        Assert.Equal(updateStatusRetryMessage.FirstSeen, republishedMessage.FirstSeen);

        deadDeliveryReportService.Verify(s => s.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeRetryMessage_WhenNotificationExpired_InsertsDeadDeliveryReportWithExpiredReason()
    {
        // Arrange
        var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationService = new Mock<IEmailNotificationService>();
        var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = emailSendOperationResult.Serialize();

        var updateStatusRetryMessage = new UpdateStatusRetryMessage
        {
            Attempts = 2,
            SendOperationResult = deliveryReport,
            FirstSeen = DateTime.UtcNow.AddSeconds(-100),
            LastAttempt = DateTime.UtcNow.AddSeconds(-5)
        };

        emailNotificationService
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .ThrowsAsync(new NotificationExpiredException(NotificationChannel.Email, "test-id", SendStatusIdentifierType.OperationId));

        deadDeliveryReportService
            .Setup(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        using var emailStatusConsumer = new EmailStatusRetryConsumer(
            kafkaProducer.Object,
            logger.Object,
            _kafkaSettings,
            emailNotificationService.Object,
            deadDeliveryReportService.Object);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, updateStatusRetryMessage.Serialize());

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    emailNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);

                    deadDeliveryReportService.Verify(
                        e => e.InsertAsync(
                        It.Is<DeadDeliveryReport>(e =>
                            e.FirstSeen == updateStatusRetryMessage.FirstSeen &&
                            e.AttemptCount == updateStatusRetryMessage.Attempts &&
                            e.Channel == DeliveryReportChannel.AzureCommunicationServices &&
                            !e.Resolved &&
                            e.DeliveryReport.Contains("NOTIFICATION_EXPIRED") &&
                            e.DeliveryReport.Contains(deliveryReport)),
                        It.IsAny<CancellationToken>()),
                        Times.Once);

                    kafkaProducer.Verify(e => e.ProduceAsync(It.Is<string>(e => e == _kafkaSettings.Value.EmailStatusUpdatedRetryTopicName), It.IsAny<string>()), Times.Never);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Called when an object is no longer needed.
    /// </summary>
    public async Task DisposeAsync()
    {
        await Dispose(true);
    }

    /// <summary>
    /// Called immediately after the class has been created, before it is used.
    /// </summary>
    public async Task InitializeAsync()
    {
        await KafkaUtil.CreateTopicAsync(_emailStatusUpdatedRetryTopicName);

        _kafkaSettings = Options.Create(new KafkaSettings
        {
            Admin = new AdminSettings()
            {
                TopicList =
                [
                    _emailStatusUpdatedRetryTopicName
                ]
            },
            BrokerAddress = "localhost:9092",
            Producer = new ProducerSettings(),
            StatusUpdatedRetryThresholdSeconds = 50,
            EmailStatusUpdatedRetryTopicName = _emailStatusUpdatedRetryTopicName,
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual async Task Dispose(bool disposing)
    {
        await KafkaUtil.DeleteTopicAsync(_emailStatusUpdatedRetryTopicName);
    }
}
