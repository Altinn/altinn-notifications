using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class NotificationStatusConsumerBaseTests : IAsyncLifetime
{
    private readonly string _smsStatusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _emailStatusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _smsStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();
    private readonly string _emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();
    private IOptions<KafkaSettings> _kafkaSettings = Options.Create(new KafkaSettings());

    /// <summary>
    /// Called immediately after the class has been created, before it is used.
    /// </summary>
    public async Task InitializeAsync()
    {
        await KafkaUtil.CreateTopicAsync(_smsStatusUpdatedTopicName);
        await KafkaUtil.CreateTopicAsync(_emailStatusUpdatedTopicName);
        await KafkaUtil.CreateTopicAsync(_smsStatusUpdatedRetryTopicName);
        await KafkaUtil.CreateTopicAsync(_emailStatusUpdatedRetryTopicName);

        _kafkaSettings = Options.Create(new KafkaSettings
        {
            Admin = new AdminSettings()
            {
                TopicList =
                [
                    _smsStatusUpdatedTopicName,
                    _emailStatusUpdatedTopicName,
                    _smsStatusUpdatedRetryTopicName,
                    _emailStatusUpdatedRetryTopicName
                ]
            },
            BrokerAddress = "localhost:9092",
            Producer = new ProducerSettings(),
            SmsStatusUpdatedTopicName = _smsStatusUpdatedTopicName,
            EmailStatusUpdatedTopicName = _emailStatusUpdatedTopicName,
            SmsStatusUpdatedRetryTopicName = _smsStatusUpdatedRetryTopicName,
            EmailStatusUpdatedRetryTopicName = _emailStatusUpdatedRetryTopicName,
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }

    /// <summary>
    /// Called when an object is no longer needed.
    /// </summary>
    public async Task DisposeAsync()
    {
        await Dispose(true);
    }

    [Fact]
    public async Task ProcessSmsDeliveryReport_WhenExceptionThrown_RepublishDeliveryReportToSameTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<SmsStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var smsNotificationRepository = new Mock<ISmsNotificationRepository>();

        var sendOperationResult = new SmsSendOperationResult
        {
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        smsNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference))
            .ThrowsAsync(new Exception("Simulated failure"));

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        var smsNotificationService = new SmsNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            smsNotificationRepository.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                SmsQueueTopicName = Guid.NewGuid().ToString()
            }),
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig() { SmsPublishBatchSize = 50 }));

        using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, smsNotificationService);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport), Times.Once);

                    logger.Verify(
                        e => e.Log(
                            LogLevel.Error,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((v, t) => true),
                            It.IsAny<Exception>(),
                            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                        Times.Once);

                    smsNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference), Times.Once);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await smsStatusConsumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenExceptionThrown_RepublishDeliveryReportToSameTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var sendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId))
            .ThrowsAsync(new Exception("Simulated failure"));

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = Guid.NewGuid().ToString()
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(e => e == deliveryReport)), Times.Once);

                    logger.Verify(
                        e => e.Log(
                            LogLevel.Error,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((v, t) => true),
                            It.IsAny<Exception>(),
                            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                        Times.Once);

                    emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

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
    public async Task ProcessSmsDeliveryReport_WhenSendStatusUpdateExceptionThrown_PublishMessageToRetryTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var republishedDeliveryReport = string.Empty;
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<SmsStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Strict);
        var smsNotificationRepository = new Mock<ISmsNotificationRepository>();

        var sendOperationResult = new SmsSendOperationResult
        {
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        smsNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Sms, sendOperationResult.GatewayReference, SendStatusIdentifierType.GatewayReference));

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedDeliveryReport = message)
            .ReturnsAsync(true);

        var smsNotificationService = new SmsNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            smsNotificationRepository.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                SmsQueueTopicName = Guid.NewGuid().ToString()
            }),
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig() { SmsPublishBatchSize = 50 }));

        using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, smsNotificationService);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                    smsNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference), Times.Once);

                    Assert.NotNull(republishedDeliveryReport);

                    var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(republishedDeliveryReport, JsonSerializerOptionsProvider.Options);

                    Assert.NotNull(retryMessage);
                    Assert.Equal(1, retryMessage.Attempts);
                    Assert.Equal(deliveryReport, retryMessage.SendOperationResult);
                    Assert.True(DateTime.UtcNow.Subtract(retryMessage.FirstSeen).TotalMinutes < 5);
                    Assert.True(DateTime.UtcNow.Subtract(retryMessage.LastAttempt).TotalMinutes < 5);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await smsStatusConsumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenSendStatusUpdateExceptionThrown_PublishMessageToRetryTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var republishedDeliveryReport = string.Empty;
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Strict);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var sendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId))
            .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, sendOperationResult.OperationId, SendStatusIdentifierType.NotificationId));

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedDeliveryReport = message)
            .ReturnsAsync(true);

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = Guid.NewGuid().ToString()
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                    emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                    Assert.NotNull(republishedDeliveryReport);

                    var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(republishedDeliveryReport, JsonSerializerOptionsProvider.Options);

                    Assert.NotNull(retryMessage);
                    Assert.Equal(1, retryMessage.Attempts);
                    Assert.Equal(deliveryReport, retryMessage.SendOperationResult);
                    Assert.True(DateTime.UtcNow.Subtract(retryMessage.FirstSeen).TotalMinutes < 5);
                    Assert.True(DateTime.UtcNow.Subtract(retryMessage.LastAttempt).TotalMinutes < 5);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

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
    public async Task ProcessSmsDeliveryReport_WhenArgumentExceptionThrown_LogDoNotRepublishDeliveryReportToSameTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var republishedDeliveryReport = string.Empty;
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<SmsStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var smsNotificationRepository = new Mock<ISmsNotificationRepository>();

        var sendOperationResult = new SmsSendOperationResult
        {
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedDeliveryReport = message)
            .ReturnsAsync(true);

        smsNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference))
            .ThrowsAsync(new ArgumentException("The provided identifier is invalid"));

        var smsNotificationService = new SmsNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            smsNotificationRepository.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                SmsQueueTopicName = Guid.NewGuid().ToString()
            }),
            Options.Create(new Altinn.Notifications.Core.Configuration.NotificationConfig() { SmsPublishBatchSize = 50 }));

        using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, smsNotificationService);

        // Act
        await smsStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    logger.Verify(
                        e => e.Log(
                            LogLevel.Error,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((v, t) => true),
                            It.IsAny<ArgumentException>(),
                            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                        Times.AtLeastOnce);

                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                    smsNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference), Times.Once);

                    Assert.Empty(republishedDeliveryReport);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15));

        await smsStatusConsumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenArgumentExceptionThrown_LogAndDoNotRepublishDeliveryReportToSameTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var republishedDeliveryReport = string.Empty;
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var sendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedDeliveryReport = message)
            .ReturnsAsync(true);

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId))
            .ThrowsAsync(new ArgumentException("The provided identifier is invalid"));

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = Guid.NewGuid().ToString()
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    logger.Verify(
                        e => e.Log(
                            LogLevel.Error,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((v, t) => true),
                            It.IsAny<ArgumentException>(),
                            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                        Times.AtLeastOnce);

                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                    emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                    Assert.Empty(republishedDeliveryReport);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

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
    public async Task ProcessEmailDeliveryReport_WhenInvalidOperationExceptionThrown_DoNotRepublishDeliveryReportToSameTopic()
    {
        // Arrange
        var publishedDeliveryReport = string.Empty;
        var guidService = new Mock<IGuidService>();
        var republishedDeliveryReport = string.Empty;
        var dateTimeService = new Mock<IDateTimeService>();
        var logger = new Mock<ILogger<EmailStatusConsumer>>();
        var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var sendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReport = sendOperationResult.Serialize();

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
            .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
            .ReturnsAsync(true);

        kafkaProducer
            .Setup(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
            .Callback<string, string>((statusUpdatedRetryTopicName, message) => republishedDeliveryReport = message)
            .ReturnsAsync(true);

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId))
            .ThrowsAsync(new InvalidOperationException("Retrieved Guid could not be parsed"));

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            kafkaProducer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = Guid.NewGuid().ToString()
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, _kafkaSettings, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await KafkaUtil.PublishMessageOnTopic(_kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                    kafkaProducer.Verify(e => e.ProduceAsync(_kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                    emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                    Assert.Empty(republishedDeliveryReport);

                    Assert.Equal(deliveryReport, publishedDeliveryReport);

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
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual async Task Dispose(bool disposing)
    {
        await KafkaUtil.DeleteTopicAsync(_smsStatusUpdatedTopicName);
        await KafkaUtil.DeleteTopicAsync(_emailStatusUpdatedTopicName);
        await KafkaUtil.DeleteTopicAsync(_smsStatusUpdatedRetryTopicName);
        await KafkaUtil.DeleteTopicAsync(_emailStatusUpdatedRetryTopicName);
    }
}
