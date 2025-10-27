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

public class NotificationStatusConsumerBaseTests
{
    [Collection("NotificationStatusConsumerBase-Test1")]
    public class ProcessSmsDeliveryReport_WhenExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessSmsDeliveryReport_WhenExceptionThrown_RepublishDeliveryReportToSameTopic()
        {
            // Arrange
            string smsStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string smsStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
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

                using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, smsNotificationService);

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport), Times.Once);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string smsStatusUpdatedTopicName, string smsStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                SmsStatusUpdatedTopicName = smsStatusUpdatedTopicName,
                SmsStatusUpdatedRetryTopicName = smsStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test2")]
    public class ProcessEmailDeliveryReport_WhenExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessEmailDeliveryReport_WhenExceptionThrown_RepublishDeliveryReportToSameTopic()
        {
            // Arrange
            string emailStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
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

                using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, emailNotificationService);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(e => e == deliveryReport)), Times.Once);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string emailStatusUpdatedTopicName, string emailStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                EmailStatusUpdatedTopicName = emailStatusUpdatedTopicName,
                EmailStatusUpdatedRetryTopicName = emailStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test3")]
    public class ProcessSmsDeliveryReport_WhenSendStatusUpdateExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessSmsDeliveryReport_WhenSendStatusUpdateExceptionThrown_PublishMessageToRetryTopic()
        {
            // Arrange
            string smsStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string smsStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
                    .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
                    .ReturnsAsync(true);

                kafkaProducer
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()))
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

                using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, smsNotificationService);

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                            smsNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference), Times.Once);

                            Assert.Empty(publishedDeliveryReport);

                            Assert.False(string.IsNullOrEmpty(republishedDeliveryReport));

                            var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(republishedDeliveryReport, JsonSerializerOptionsProvider.Options);
                            Assert.NotNull(retryMessage);
                            Assert.Equal(1, retryMessage!.Attempts);
                            Assert.Equal(deliveryReport, retryMessage.SendOperationResult);
                            Assert.True(DateTime.UtcNow.Subtract(retryMessage.FirstSeen).TotalMinutes < 5);
                            Assert.True(DateTime.UtcNow.Subtract(retryMessage.LastAttempt).TotalMinutes < 5);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string smsStatusUpdatedTopicName, string smsStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                SmsStatusUpdatedTopicName = smsStatusUpdatedTopicName,
                SmsStatusUpdatedRetryTopicName = smsStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test4")]
    public class ProcessEmailDeliveryReport_WhenSendStatusUpdateExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessEmailDeliveryReport_WhenSendStatusUpdateExceptionThrown_PublishMessageToRetryTopic()
        {
            // Arrange
            string emailStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
                    .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
                    .ReturnsAsync(true);

                kafkaProducer
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
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

                using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, emailNotificationService);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                            emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                            Assert.Empty(publishedDeliveryReport);

                            Assert.False(string.IsNullOrEmpty(republishedDeliveryReport));

                            var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(republishedDeliveryReport, JsonSerializerOptionsProvider.Options);
                            Assert.NotNull(retryMessage);
                            Assert.Equal(1, retryMessage!.Attempts);
                            Assert.Equal(deliveryReport, retryMessage.SendOperationResult);
                            Assert.True(DateTime.UtcNow.Subtract(retryMessage.FirstSeen).TotalMinutes < 5);
                            Assert.True(DateTime.UtcNow.Subtract(retryMessage.LastAttempt).TotalMinutes < 5);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string emailStatusUpdatedTopicName, string emailStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                EmailStatusUpdatedTopicName = emailStatusUpdatedTopicName,
                EmailStatusUpdatedRetryTopicName = emailStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test5")]
    public class ProcessSmsDeliveryReport_WhenArgumentExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessSmsDeliveryReport_WhenArgumentExceptionThrown_DoNotRepublishDeliveryReportToSameTopic()
        {
            // Arrange
            string smsStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string smsStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(smsStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport))
                    .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
                    .ReturnsAsync(true);

                kafkaProducer
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()))
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

                using var smsStatusConsumer = new SmsStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, smsNotificationService);

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.SmsStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                            smsNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference), Times.Once);

                            Assert.Empty(publishedDeliveryReport);

                            Assert.Empty(republishedDeliveryReport);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(smsStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string smsStatusUpdatedTopicName, string smsStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [smsStatusUpdatedTopicName, smsStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                SmsStatusUpdatedTopicName = smsStatusUpdatedTopicName,
                SmsStatusUpdatedRetryTopicName = smsStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test6")]
    public class ProcessEmailDeliveryReport_WhenArgumentExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessEmailDeliveryReport_WhenArgumentExceptionThrown_DoNotRepublishDeliveryReportToSameTopic()
        {
            // Arrange
            string emailStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
                    .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
                    .ReturnsAsync(true);

                kafkaProducer
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
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

                using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, emailNotificationService);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                            emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                            Assert.Empty(publishedDeliveryReport);

                            Assert.Empty(republishedDeliveryReport);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string emailStatusUpdatedTopicName, string emailStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                EmailStatusUpdatedTopicName = emailStatusUpdatedTopicName,
                EmailStatusUpdatedRetryTopicName = emailStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }

    [Collection("NotificationStatusConsumerBase-Test7")]
    public class ProcessEmailDeliveryReport_WhenInvalidOperationExceptionThrown_Tests
    {
        [Fact]
        public async Task ProcessEmailDeliveryReport_WhenInvalidOperationExceptionThrown_DoNotRepublishDeliveryReportToSameTopic()
        {
            // Arrange
            string emailStatusUpdatedTopicName = Guid.NewGuid().ToString();
            string emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(emailStatusUpdatedRetryTopicName);

                var kafkaSettings = BuildKafkaSettings(emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName);

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
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport))
                    .Callback<string, string>((statusUpdatedTopicName, message) => publishedDeliveryReport = message)
                    .ReturnsAsync(true);

                kafkaProducer
                    .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
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

                using var emailStatusConsumer = new EmailStatusConsumer(kafkaProducer.Object, logger.Object, kafkaSettings, emailNotificationService);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReport);

                // Assert
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.IsAny<string>()), Times.Never);

                            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

                            emailNotificationRepository.Verify(e => e.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult.Value, sendOperationResult.OperationId), Times.Once);

                            Assert.Empty(publishedDeliveryReport);

                            Assert.Empty(republishedDeliveryReport);

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
            finally
            {
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(emailStatusUpdatedRetryTopicName);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string emailStatusUpdatedTopicName, string emailStatusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                Admin = new AdminSettings()
                {
                    TopicList = [emailStatusUpdatedTopicName, emailStatusUpdatedRetryTopicName]
                },
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                EmailStatusUpdatedTopicName = emailStatusUpdatedTopicName,
                EmailStatusUpdatedRetryTopicName = emailStatusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }
}
