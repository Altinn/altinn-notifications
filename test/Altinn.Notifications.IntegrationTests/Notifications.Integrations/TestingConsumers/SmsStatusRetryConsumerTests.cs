using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
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
    public class SmsStatusRetryConsumerTests
    {
        [Collection("SmsStatusRetryConsumer-Test1")]
        public class MessageOnRetryTopic_IsPersisted_Tests
        {
            [Fact]
            public async Task MessageOnRetryTopic_IsPersisted_WhenThresholdTimeHasElapsed()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();
                long? deadDeliveryReportIdentifier = null;

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    Dictionary<string, string> kafkaSettings = new()
                    {
                        { "KafkaSettings__SmsStatusUpdatedRetryTopicName", statusUpdatedRetryTopicName },
                        { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedRetryTopicName}\"]" }
                    };

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = ServiceUtil
                        .GetServices([typeof(IHostedService)], kafkaSettings)
                        .OfType<SmsStatusRetryConsumer>()
                        .First();

                    var smsSendOperationResult = new SmsSendOperationResult
                    {
                        NotificationId = Guid.NewGuid(),
                        GatewayReference = Guid.NewGuid().ToString(),
                        SendResult = SmsNotificationResultType.Delivered
                    };

                    var retryMessage = new UpdateStatusRetryMessage
                    {
                        Attempts = 15,
                        FirstSeen = DateTime.UtcNow.AddSeconds(-600),
                        LastAttempt = DateTime.UtcNow.AddSeconds(-15),
                        SendOperationResult = smsSendOperationResult.Serialize()
                    };

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, retryMessage.Serialize());
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

                    await IntegrationTestUtil.EventuallyAsync(
                        async () =>
                        {
                            deadDeliveryReportIdentifier = await PostgreUtil.GetDeadDeliveryReportIdFromGatewayReference(smsSendOperationResult.GatewayReference);

                            return deadDeliveryReportIdentifier != null;
                        },
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromMilliseconds(100));

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.NotNull(deadDeliveryReportIdentifier);
                    Assert.True(deadDeliveryReportIdentifier > 0);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);

                    if (deadDeliveryReportIdentifier.HasValue)
                    {
                        await CleanupDeadDeliveryReportAsync(deadDeliveryReportIdentifier.Value);
                    }
                }
            }

            private static async Task CleanupDeadDeliveryReportAsync(long deadDeliveryReportId)
            {
                string deleteSql = $@"DELETE from notifications.deaddeliveryreports where id = @id";

                NpgsqlParameter[] parameters =
                [
                    new("id", deadDeliveryReportId)
                ];

                await PostgreUtil.RunSql(deleteSql, parameters);
            }
        }

        [Collection("SmsStatusRetryConsumer-Test2")]
        public class ProcessMessage_InvalidJson_Tests
        {
            [Fact]
            public async Task ProcessMessage_InvalidJson_IgnoresMessage_NoRetry_NoPersistence()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var errorIsLogged = false;
                    var smsService = new Mock<ISmsNotificationService>();
                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        kafkaProducer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsService.Object,
                        deadDeliveryReportService.Object);

                    string invalidPayload = "not-a-json";

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, invalidPayload);
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

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

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.True(errorIsLogged);
                    smsService.Verify(e => e.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Never);
                    kafkaProducer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }

        [Collection("SmsStatusRetryConsumer-Test3")]
        public class ProcessMessage_EmptySendOperationResult_Tests
        {
            [Fact]
            public async Task ProcessMessage_EmptySendOperationResult_LogsErrorAndIgnoresMessage()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var errorIsLogged = false;
                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var smsNotificationService = new Mock<ISmsNotificationService>();
                    var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        kafkaProducer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsNotificationService.Object,
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
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, updateStatusRetryMessageSerialized);
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

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

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.True(errorIsLogged);
                    kafkaProducer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    smsNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Never);
                    deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }

        [Collection("SmsStatusRetryConsumer-Test4")]
        public class ProcessMessage_AttemptsToUpdateStatus_Tests
        {
            [Fact]
            public async Task ProcessMessage_AttemptsToUpdateStatus_WhenThresholdTimeHasNotElapsed()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var smsNotificationServiceMock = new Mock<ISmsNotificationService>();

                    var smsSendOperationResult = new SmsSendOperationResult
                    {
                        NotificationId = Guid.NewGuid(),
                        GatewayReference = Guid.NewGuid().ToString(),
                        SendResult = SmsNotificationResultType.Delivered
                    };

                    smsNotificationServiceMock
                        .Setup(e => e.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
                        .Returns(Task.CompletedTask);

                    var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        producer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsNotificationServiceMock.Object,
                        deadDeliveryReportServiceMock.Object);

                    var retryMessage = new UpdateStatusRetryMessage
                    {
                        Attempts = 1,
                        LastAttempt = DateTime.UtcNow,
                        FirstSeen = DateTime.UtcNow.AddSeconds(-30),
                        SendOperationResult = smsSendOperationResult.Serialize()
                    };

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, retryMessage.Serialize());
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

                    var statusUpdateSucceeded = false;
                    await IntegrationTestUtil.EventuallyAsync(
                        () =>
                        {
                            try
                            {
                                smsNotificationServiceMock.Verify(
                                    e => e.UpdateSendStatus(
                                        It.Is<SmsSendOperationResult>(result => result.GatewayReference == smsSendOperationResult.GatewayReference && result.SendResult == smsSendOperationResult.SendResult)),
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

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

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
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }

        [Collection("SmsStatusRetryConsumer-Test5")]
        public class ProcessMessage_IncrementsAttemptsAndPublishesRetry_Tests
        {
            [Fact]
            public async Task ProcessMessage_IncrementsAttemptsAndPublishesRetry_WhenUpdateFailsBeforeThreshold()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var smsNotificationService = new Mock<ISmsNotificationService>();
                    UpdateStatusRetryMessage? republishedUpdateStatusRetryMessage = null;
                    var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

                    var smsSendOperationResult = new SmsSendOperationResult
                    {
                        NotificationId = Guid.NewGuid(),
                        GatewayReference = Guid.NewGuid().ToString(),
                        SendResult = SmsNotificationResultType.Delivered
                    };

                    var originalUpdateStatusRetryMessage = new UpdateStatusRetryMessage
                    {
                        Attempts = 3,
                        FirstSeen = DateTime.UtcNow.AddSeconds(-25),
                        LastAttempt = DateTime.UtcNow.AddSeconds(-5),
                        SendOperationResult = smsSendOperationResult.Serialize()
                    };

                    smsNotificationService
                        .Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
                        .ThrowsAsync(new Exception());

                    kafkaProducer
                        .Setup(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()))
                        .Callback<string, string>((statusUpdatedRetryTopicName, payload) =>
                        {
                            republishedUpdateStatusRetryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(payload, JsonSerializerOptionsProvider.Options);
                        })
                        .ReturnsAsync(true);

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        kafkaProducer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsNotificationService.Object,
                        deadDeliveryReportService.Object);

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, originalUpdateStatusRetryMessage.Serialize());
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

                    await IntegrationTestUtil.EventuallyAsync(
                        () =>
                        {
                            return republishedUpdateStatusRetryMessage is not null;
                        },
                        TimeSpan.FromSeconds(8),
                        TimeSpan.FromMilliseconds(150));

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.NotNull(republishedUpdateStatusRetryMessage);
                    Assert.Equal(originalUpdateStatusRetryMessage.FirstSeen, republishedUpdateStatusRetryMessage.FirstSeen);
                    Assert.Equal(originalUpdateStatusRetryMessage.Attempts + 1, republishedUpdateStatusRetryMessage!.Attempts);
                    Assert.True(republishedUpdateStatusRetryMessage.LastAttempt > originalUpdateStatusRetryMessage.LastAttempt);
                    Assert.Equal(originalUpdateStatusRetryMessage.SendOperationResult, republishedUpdateStatusRetryMessage.SendOperationResult);

                    smsNotificationService.Verify(e => e.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Once);

                    kafkaProducer.Verify(e => e.ProduceAsync(kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, It.IsAny<string>()), Times.Once);

                    deadDeliveryReportService.Verify(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }

        [Collection("SmsStatusRetryConsumer-Test6")]
        public class ProcessMessage_NullDeserializedSmsSendOperationResult_Tests
        {
            [Fact]
            public async Task ProcessMessage_NullDeserializedSmsSendOperationResult_LogsErrorAndIgnoresMessage()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var smsNotificationService = new Mock<ISmsNotificationService>();
                    var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

                    var retryMessage = new UpdateStatusRetryMessage
                    {
                        Attempts = 1,
                        FirstSeen = DateTime.UtcNow.AddSeconds(-5),
                        LastAttempt = DateTime.UtcNow.AddSeconds(-2),
                        SendOperationResult = "null"
                    };

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        kafkaProducer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsNotificationService.Object,
                        deadDeliveryReportService.Object);

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, retryMessage.Serialize());
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

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
                                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SmsSendOperationResult deserialization returned null")),
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

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.True(logVerified);
                    smsNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Never);
                    kafkaProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    deadDeliveryReportService.Verify(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }

        [Collection("SmsStatusRetryConsumer-Test7")]
        public class ProcessMessage_MalformedJson_Tests
        {
            [Fact]
            public async Task ProcessMessage_MalformedJson_LogsErrorAndDoesNotRetry()
            {
                // Arrange
                var statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

                try
                {
                    await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                    var logVerified = false;
                    var logger = new Mock<ILogger<SmsStatusRetryConsumer>>();
                    var kafkaProducer = new Mock<IKafkaProducer>(MockBehavior.Loose);
                    var kafkaSettings = BuildKafkaSettings(statusUpdatedRetryTopicName);
                    var smsNotificationService = new Mock<ISmsNotificationService>();
                    var deadDeliveryReportService = new Mock<IDeadDeliveryReportService>();

                    var retryMessage = new UpdateStatusRetryMessage
                    {
                        Attempts = 1,
                        FirstSeen = DateTime.UtcNow.AddSeconds(-5),
                        LastAttempt = DateTime.UtcNow.AddSeconds(-2),
                        SendOperationResult = "{\"invalid\": json syntax}"
                    };

                    using SmsStatusRetryConsumer smsStatusRetryConsumer = new(
                        kafkaProducer.Object,
                        logger.Object,
                        kafkaSettings,
                        smsNotificationService.Object,
                        deadDeliveryReportService.Object);

                    // Act
                    await KafkaUtil.PublishMessageOnTopic(statusUpdatedRetryTopicName, retryMessage.Serialize());
                    await smsStatusRetryConsumer.StartAsync(CancellationToken.None);

                    await IntegrationTestUtil.EventuallyAsync(
                        () =>
                        {
                            try
                            {
                                logger.Verify(
                                    e => e.Log(
                                        LogLevel.Error,
                                        It.IsAny<EventId>(),
                                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SmsSendOperationResult deserialization failed")),
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

                    await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

                    // Assert
                    Assert.True(logVerified);
                    smsNotificationService.Verify(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Never);
                    kafkaProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    deadDeliveryReportService.Verify(d => d.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()), Times.Never);
                }
                finally
                {
                    await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
                }
            }

            private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
            {
                return Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    StatusUpdatedRetryThresholdSeconds = 50,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });
            }
        }
    }
}
