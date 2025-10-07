using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers
{
    public class SmsStatusRetryConsumerTests : IAsyncLifetime
    {
        private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();
        private readonly List<long> _deadDeliveryReportdIds = [];

        [Fact]
        public async Task MessageOnRetryTopic_IsPersisted_WhenThresholdTimeHasElapsed()
        {
            // Arrange
            long? id = null;

            Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__SmsStatusUpdatedRetryTopicName", _statusUpdatedRetryTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedRetryTopicName}\"]" }
        };

            using SmsStatusRetryConsumer smsStatusRetryConsumer = ServiceUtil
                .GetServices([typeof(IHostedService)], vars)
                .OfType<SmsStatusRetryConsumer>()
                .First();

            // use this to verify that the message was persisted
            var smsSendOperationResult = new SmsSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = Guid.NewGuid().ToString(),
                SendResult = SmsNotificationResultType.Delivered
            };

            var retryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow.AddMinutes(-10), // should hit threshold
                NotificationId = Guid.NewGuid(),
                ExternalReferenceId = Guid.NewGuid(),
                SendResult = smsSendOperationResult.Serialize()
            };

            // Act
            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());

            await smsStatusRetryConsumer.StartAsync(CancellationToken.None);
            await Task.Delay(250);

            await KafkaUtilityFunctions.EventuallyAsync(
                async () =>
                {
                    id = await PostgreUtil.GetDeadDeliveryReportIdFromGatewayReference(smsSendOperationResult.GatewayReference);
                    return id != null;
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(1000));
            await smsStatusRetryConsumer.StopAsync(CancellationToken.None);

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
    }
}
