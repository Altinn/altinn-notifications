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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
        public async Task ProcessMessage_IncrementsAttemptsAndRetriesMessage_WhenUpdateFailsAndThresholdNotElapsed()
        {
            // Arrange
            var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationServiceMock = new Mock<IEmailNotificationService>();

            // use this to verify that the message was not persisted
            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            emailNotificationServiceMock.Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, emailSendOperationResult.OperationId, SendStatusIdentifierType.OperationId));

            var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                producer.Object,
                NullLogger<EmailStatusRetryConsumer>.Instance,
                kafkaSettings,
                emailNotificationServiceMock.Object,
                deadDeliveryReportServiceMock.Object);

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow, // should NOT hit threshold
                LastAttempt = DateTime.UtcNow,
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
          () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                              i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedRetryTopicName &&
                                              i.Arguments[1] is string message && !string.IsNullOrWhiteSpace(message) && JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options)?.Attempts == retryMessage.Attempts + 1),
          TimeSpan.FromSeconds(10),
          TimeSpan.FromMilliseconds(1000));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ProcessMessage_AttemptsToUpdateStatus_WhenThresholdTimeHasNotElapsed()
        {
            // Arrange
            var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);
            var emailNotificationServiceMock = new Mock<IEmailNotificationService>();

            // use this to verify that the message was not persisted
            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            emailNotificationServiceMock.Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                .Returns(Task.CompletedTask);

            var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                producer.Object,
                NullLogger<EmailStatusRetryConsumer>.Instance,
                kafkaSettings,
                emailNotificationServiceMock.Object,
                deadDeliveryReportServiceMock.Object);

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow, // should NOT hit threshold
                LastAttempt = DateTime.UtcNow,
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                () => emailNotificationServiceMock.Invocations.Any(i => i.Method.Name == nameof(IEmailNotificationService.UpdateSendStatus)) && producer.Invocations.Count == 0,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(1000));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            emailNotificationServiceMock.Verify(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Once);
        }

        [Fact]
        public async Task MessageOnRetryTopic_IsPersisted_WhenThresholdTimeHasElapsed()
        {
            // Arrange
            long? id = null;

            Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedRetryTopicName", _statusUpdatedRetryTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedRetryTopicName}\"]" }
        };

            using EmailStatusRetryConsumer emailStatusRetryConsumer = ServiceUtil
                .GetServices([typeof(IHostedService)], vars)
                .OfType<EmailStatusRetryConsumer>()
                .First();

            // use this to verify that the message was persisted
            var emailSendOperationResult = new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered
            };

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow.AddMinutes(-10), // should hit threshold
                LastAttempt = DateTime.UtcNow,
                SendOperationResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);

            await IntegrationTestUtil.EventuallyAsync(
                async () =>
                {
                    id = await PostgreUtil.GetDeadDeliveryReportIdFromOperationId(emailSendOperationResult.OperationId);
                    return id != null;
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(1000));
            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(id);
            Assert.True(id > 0);

            // Cleanup
            if (id.HasValue)
            {
                _deadDeliveryReportIds.Add(id.Value);
            }
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

        /// <summary>
        /// Creates Kafka settings.
        /// </summary>
        /// <returns>
        /// An <see cref="IOptions{KafkaSettings}"/> instance with minimal configuration needed for running the notification consumer tests.
        /// </returns>
        /// <remarks>
        /// Provides a standard configuration with localhost broker address and unit-tests group ID.
        /// </remarks>
        private static IOptions<KafkaSettings> BuildKafkaSettings(string statusUpdatedRetryTopicName)
        {
            return Options.Create(new KafkaSettings
            {
                BrokerAddress = "localhost:9092",
                Producer = new ProducerSettings(),
                EmailStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }
}
