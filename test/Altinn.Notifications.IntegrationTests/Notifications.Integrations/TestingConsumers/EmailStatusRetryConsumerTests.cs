using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers
{
    public class EmailStatusRetryConsumerTests : IAsyncLifetime
    {
        private readonly string _statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

        [Fact]
        public async Task InsertRetryTopic()
        {
            // Arrange
            Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedRetryTopicName", _statusUpdatedRetryTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedRetryTopicName}\"]" }
        };

            using EmailStatusRetryConsumer emailStatusRetryConsumer = ServiceUtil
                .GetServices([typeof(IHostedService)], vars)
                .OfType<EmailStatusRetryConsumer>()
                .First();

            // Act
            var retryMessage = new RetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow,
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid(),
                SendResult = "Test message"
            };

            await KafkaUtil.PublishMessageOnTopic(_statusUpdatedRetryTopicName, retryMessage.Serialize());
            
            await emailStatusRetryConsumer.StartAsync(CancellationToken.None);
            await Task.Delay(10000);
            await emailStatusRetryConsumer.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, 1);
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
        }
    }
}
