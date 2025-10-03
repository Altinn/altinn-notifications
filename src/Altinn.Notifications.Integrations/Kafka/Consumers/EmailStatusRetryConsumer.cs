using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Integrations.Kafka.Consumers
{
    /// <summary>
    /// Kafka consumer for processing email status retry messages
    /// </summary>
    public sealed class EmailStatusRetryConsumer : KafkaConsumerBase<EmailStatusRetryConsumer>
    {
        /// <summary>
        /// Executes the email status retry consumer to process messages from the Kafka topic
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
        }
    }   
}
