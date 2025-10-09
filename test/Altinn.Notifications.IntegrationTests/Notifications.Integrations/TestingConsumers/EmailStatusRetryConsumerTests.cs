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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers
{
    public class EmailStatusRetryConsumerTests : IAsyncLifetime
    {
        private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();
        private readonly List<long> _deadDeliveryReportdIds = [];

        [Fact]
        public async Task MessageOnRetryTopic_AttemptsIsIncrementedAndRetried_WhenThresholdTimeHasNotElapsed()
        {
            // Arrange
            var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
            var kafkaSettings = BuildKafkaSettings(_statusUpdatedRetryTopicName);

            var deadDeliveryReportServiceMock = new Mock<IDeadDeliveryReportService>();
            using EmailStatusRetryConsumer emailStatusRetryConsumer = new(
                producer.Object,
                deadDeliveryReportServiceMock.Object,
                kafkaSettings,
                NullLogger<EmailStatusRetryConsumer>.Instance);

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
                FirstSeen = DateTime.UtcNow, // should NOT hit threshold
                NotificationId = Guid.NewGuid(),
                ExternalReferenceId = Guid.NewGuid(),
                SendResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);
            await Task.Delay(250);

            await IntegrationTestUtil.EventuallyAsync(
          () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                              i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedRetryTopicName &&
                                              i.Arguments[1] is string message && !string.IsNullOrWhiteSpace(message) && JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options)?.Attempts == retryMessage.Attempts + 1),
          TimeSpan.FromSeconds(10),
          TimeSpan.FromMilliseconds(1000));

            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);
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
                NotificationId = Guid.NewGuid(),
                ExternalReferenceId = Guid.NewGuid(),
                SendResult = emailSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);
            await Task.Delay(250);

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
                _deadDeliveryReportdIds.Add(id.Value);
            }
        }

        public async Task DisposeAsync()
        {
            await Dispose(true);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual async Task Dispose(bool disposing)
        {
            await KafkaUtil.DeleteTopicAsync(_statusUpdatedRetryTopicName);
            await CleanupDeadDeliveryReportsAsync();
        }

        private async Task CleanupDeadDeliveryReportsAsync()
        {
            if (_deadDeliveryReportdIds.Count != 0)
            {
                string deleteSql = $@"DELETE from notifications.deaddeliveryreports where id = ANY(@ids)";
                NpgsqlParameter[] parameters =
                [
                    new("ids", _deadDeliveryReportdIds.ToArray())
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
                EmailStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                Producer = new ProducerSettings(),
                Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
            });
        }
    }
}
