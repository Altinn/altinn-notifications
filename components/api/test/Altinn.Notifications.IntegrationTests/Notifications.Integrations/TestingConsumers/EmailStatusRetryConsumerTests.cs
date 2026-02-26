using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers
{
    public class EmailStatusRetryConsumerTests : IAsyncLifetime
    {
        private readonly List<long> _deadDeliveryReportIds = [];
        private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

        [Fact]
        public async Task MessageOnRetryTopic_IsPersisted_WhenThresholdTimeHasElapsed()
        {
            // Arrange
            long? deadDeliveryReportIdentifier = null;

            Dictionary<string, string> kafkaSettings = new()
            {
                { "KafkaSettings__EmailStatusUpdatedRetryTopicName", _statusUpdatedRetryTopicName },
                { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedRetryTopicName}\"]" }
            };

            using EmailStatusRetryConsumer emailStatusRetryConsumer = ServiceUtil
                .GetServices([typeof(IHostedService)], kafkaSettings)
                .OfType<EmailStatusRetryConsumer>()
                .First();

            var emailSendOperationResult = new EmailSendOperationResult
            {
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 15,
                FirstSeen = DateTime.UtcNow.AddSeconds(-600),
                LastAttempt = DateTime.UtcNow.AddSeconds(-15),
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                async () =>
                {
                    deadDeliveryReportIdentifier = await PostgreUtil.GetDeadDeliveryReportIdFromOperationId(emailSendOperationResult.OperationId);

                    return deadDeliveryReportIdentifier != null;
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(deadDeliveryReportIdentifier);
            Assert.True(deadDeliveryReportIdentifier > 0);

            // Clean up
            if (deadDeliveryReportIdentifier.HasValue)
            {
                _deadDeliveryReportIds.Add(deadDeliveryReportIdentifier.Value);
            }
        }

        [Fact]
        public async Task ProcessMessage_InvalidJson_IgnoresMessage_NoRetry_NoPersistence()
        {
            // Arrange
            var errorIsLogged = false;
            var emailService = new Mock<IEmailNotificationService>();
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailService.Object,
                deadDeliveryReportService.Object);

            string invalidPayload = "not-a-json";

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, invalidPayload);
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    logger.Verify(
                        e => e.Log(
                            LogLevel.Error,
                            It.IsAny<EventId>(),
                            It.IsAny<It.IsAnyType>(),
                            It.IsAny<Exception?>(),
                            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                        Times.Once);

                    errorIsLogged = true;

                    return errorIsLogged;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(errorIsLogged);
            kafkaProducer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            emailService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
            deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessMessage_EmptySendOperationResult_LogsErrorAndIgnoresMessage()
        {
            // Arrange
            var errorIsLogged = false;
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationService = new Mock<IEmailNotificationService>();
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationService.Object,
                deadDeliveryReportService.Object);

            var updateStatusRetryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                SendOperationResult = string.Empty,
                FirstSeen = DateTime.UtcNow.AddSeconds(-10),
                LastAttempt = DateTime.UtcNow.AddSeconds(-5)
            };

            var updateStatusRetryMessageSerialized = updateStatusRetryMessage.Serialize();

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, updateStatusRetryMessageSerialized);
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    try
                    {
                        logger.Verify(
                            e => e.Log(
                                LogLevel.Error,
                                It.IsAny<EventId>(),
                                It.IsAny<It.IsAnyType>(),
                                It.IsAny<Exception?>(),
                                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                            Times.Once);

                        errorIsLogged = true;

                        return errorIsLogged;
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(errorIsLogged);
            kafkaProducer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            emailNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
            deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessMessage_AttemptsToUpdateStatus_WhenThresholdTimeHasNotElapsed()
        {
            // Arrange
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationServiceMock = new Mock<IEmailNotificationService>();

            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            emailNotificationServiceMock
                .Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                .Returns(Task.CompletedTask);

            var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                producer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationServiceMock.Object,
                deadDeliveryReportServiceMock.Object);

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                LastAttempt = DateTime.UtcNow,
                FirstSeen = DateTime.UtcNow.AddSeconds(-30),
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            var statusUpdateSucceeded = false;
            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    try
                    {
                        emailNotificationServiceMock.Verify(
                            e => e.UpdateSendStatus(
                                It.Is<EmailSendOperationResult>(result => result.OperationId == emailSendOperationResult.OperationId && result.SendResult == emailSendOperationResult.SendResult)),
                            Times.Once);

                        statusUpdateSucceeded = true;

                        return statusUpdateSucceeded;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(1000));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(statusUpdateSucceeded);

            logger.Verify(
                e => e.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception?>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((_, __) => true)),
                Times.Never);

            producer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            deadDeliveryReportServiceMock.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessMessage_IncrementsAttemptsAndPublishesRetry_WhenUpdateFailsBeforeThreshold()
        {
            // Arrange
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationService = new Mock<IEmailNotificationService>();
            UpdateStatusRetryMessage? republishedUpdateStatusRetryMessage = null;
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            var originalUpdateStatusRetryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 3,
                FirstSeen = DateTime.UtcNow.AddSeconds(-25),
                LastAttempt = DateTime.UtcNow.AddSeconds(-5),
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            emailNotificationService
                .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                .ThrowsAsync(new Exception());

            kafkaProducer
                .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()))
                .Callback<string, string>((statusUpdatedRetryTopicName, payload) =>
                {
                    republishedUpdateStatusRetryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(payload, JsonSerializerOptionsProvider.Options);
                })
                .ReturnsAsync(true);

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationService.Object,
                deadDeliveryReportService.Object);

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, originalUpdateStatusRetryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    return republishedUpdateStatusRetryMessage is not null;
                },
                TimeSpan.FromSeconds(8),
                TimeSpan.FromMilliseconds(150));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(republishedUpdateStatusRetryMessage);
            Assert.Equal(originalUpdateStatusRetryMessage.FirstSeen, republishedUpdateStatusRetryMessage.FirstSeen);
            Assert.Equal(originalUpdateStatusRetryMessage.Attempts + 1, republishedUpdateStatusRetryMessage!.Attempts);
            Assert.True(republishedUpdateStatusRetryMessage.LastAttempt > originalUpdateStatusRetryMessage.LastAttempt);
            Assert.Equal(originalUpdateStatusRetryMessage.SendOperationResult, republishedUpdateStatusRetryMessage.SendOperationResult);

            emailNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);

            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

            deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessMessage_NullDeserializedEmailSendOperationResult_LogsErrorAndIgnoresMessage()
        {
            // Arrange
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationService = new Mock<IEmailNotificationService>();
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow.AddSeconds(-5),
                LastAttempt = DateTime.UtcNow.AddSeconds(-2),
                SendOperationResult = "null"
            };

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationService.Object,
                deadDeliveryReportService.Object);

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            var logVerified = false;
            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    try
                    {
                        logger.Verify(
                            e => e.Log(
                                LogLevel.Error,
                                It.IsAny<EventId>(),
                                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("EmailSendOperationResult deserialization returned null")),
                                It.IsAny<Exception?>(),
                                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                            Times.Once);

                        logVerified = true;

                        return logVerified;
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(logVerified);
            emailNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
            kafkaProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            deadDeliveryReportService.Verify(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessMessage_MalformedJson_LogsErrorAndDoesNotRetry()
        {
            // Arrange
            var logVerified = false;
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationService = new Mock<IEmailNotificationService>();
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow.AddSeconds(-5),
                LastAttempt = DateTime.UtcNow.AddSeconds(-2),
                SendOperationResult = "{\"invalid\": json syntax}"
            };

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationService.Object,
                deadDeliveryReportService.Object);

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    try
                    {
                        logger.Verify(
                            e => e.Log(
                                LogLevel.Error,
                                It.IsAny<EventId>(),
                                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("EmailSendOperationResult deserialization failed")),
                                It.IsAny<JsonException>(),
                                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                            Times.Once);

                        logVerified = true;

                        return logVerified;
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(logVerified);
            emailNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
            kafkaProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            deadDeliveryReportService.Verify(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(SendStatusIdentifierType.OperationId)]
        [InlineData(SendStatusIdentifierType.NotificationId)]
        public async Task ProcessMessage_WhenNotificationExpired_SavesDeadDeliveryReportAndDoesNotRetry(SendStatusIdentifierType identifierType)
        {
            // Arrange
            var logger = new Mock<ILogger<EmailStatusRetryConsumer>>();
            var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationService = new Mock<IEmailNotificationService>();
            var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            var originalUpdateStatusRetryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 5,
                FirstSeen = DateTime.UtcNow.AddSeconds(-30),
                LastAttempt = DateTime.UtcNow.AddSeconds(-5),
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            string identifierValue = identifierType == SendStatusIdentifierType.NotificationId
                ? emailSendOperationResult.NotificationId.ToString()!
                : emailSendOperationResult.OperationId ?? string.Empty;

            emailNotificationService
                .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                .ThrowsAsync(new NotificationExpiredException(NotificationChannel.Email, identifierValue, identifierType));

            DeadDeliveryReport? capturedDeadDeliveryReport = null;
            deadDeliveryReportService
                .Setup(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()))
                .Callback<DeadDeliveryReport, CancellationToken>((report, _) => capturedDeadDeliveryReport = report)
                .ReturnsAsync(1L);

            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                kafkaProducer.Object,
                logger.Object,
                kafkaSettings,
                emailNotificationService.Object,
                deadDeliveryReportService.Object);

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, originalUpdateStatusRetryMessage.Serialize());
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                () =>
                {
                    return capturedDeadDeliveryReport != null;
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(100));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(capturedDeadDeliveryReport);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, capturedDeadDeliveryReport!.Channel);
            Assert.Equal(originalUpdateStatusRetryMessage.FirstSeen, capturedDeadDeliveryReport.FirstSeen);
            Assert.Equal(originalUpdateStatusRetryMessage.Attempts, capturedDeadDeliveryReport.AttemptCount);
            Assert.False(capturedDeadDeliveryReport.Resolved);
            Assert.Equal("NOTIFICATION_EXPIRED", capturedDeadDeliveryReport.Reason);
            Assert.Equal("Notification expiry time has passed", capturedDeadDeliveryReport.Message);
            Assert.Contains(emailSendOperationResult.NotificationId.ToString()!, capturedDeadDeliveryReport.DeliveryReport);
            Assert.Contains(emailSendOperationResult.OperationId!, capturedDeadDeliveryReport.DeliveryReport);

            // Verify update was attempted
            emailNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);

            // Verify no retry was published
            kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Never);

            // Verify dead delivery report was saved
            deadDeliveryReportService.Verify(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        public async Task DisposeAsync()
        {
            await Dispose(true);
        }

        public async Task InitializeAsync()
        {
            await KafkaUtil.CreateTopicAsync(_statusUpdatedRetryTopicName);
        }

        protected virtual async Task Dispose(bool disposing)
        {
            await KafkaUtil.DeleteTopicAsync(_statusUpdatedRetryTopicName);

            await CleanupDeadDeliveryReportsAsync();
        }

        private async Task CleanupDeadDeliveryReportsAsync()
        {
            if (_deadDeliveryReportIds.Count != 0)
            {
                string deleteSql = $@"DELETE from notifications.deaddeliveryreports where id = ANY(@ids)";

                NpgsqlParameter[] parameters =
                [
                    new("ids", _deadDeliveryReportIds.ToArray())
                ];

                await PostgreUtil.RunSql(deleteSql, parameters);
            }
        }

        private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                StatusUpdatedRetryThresholdSeconds = 50,
                EmailStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }
}
