using Altinn.Notifications.Core.AltinnServiceUpdate;
using Altinn.Notifications.Core.ServiceUpdate;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers
{
    /// <summary>
    /// Kafka consumer class for platform service updates
    /// </summary>
    public class AltinnServiceUpdateConsumer : KafkaConsumerBase<AltinnServiceUpdateConsumer>
    {
        private readonly IAltinnServiceUpdateService _serviceUpdate;

        /// <summary>
        /// Initializes a new instance of the <see cref="AltinnServiceUpdateConsumer"/> class.
        /// </summary>
        public AltinnServiceUpdateConsumer(
            IAltinnServiceUpdateService serviceUpdate,
            IOptions<KafkaSettings> settings,
            ILogger<AltinnServiceUpdateConsumer> logger)
            : base(settings, logger, settings.Value.AltinnServiceUpdateTopicName)
        {
            _serviceUpdate = serviceUpdate;
        }

        /// <inheritdoc/>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => ConsumeMessage(ProcessServiceUpdate, RetryServiceUpdate, stoppingToken), stoppingToken);
        }

        private async Task ProcessServiceUpdate(string message)
        {
            bool succeeded = GenericServiceUpdate.TryParse(message, out GenericServiceUpdate update);

            if (!succeeded)
            {
                return;
            }

            await _serviceUpdate.HandleServiceUpdate(update.Source, update.Schema, update.Data);
        }

        private async Task RetryServiceUpdate(string message)
        {
            // making two attempts, but no further action if it fails
            await ProcessServiceUpdate(message);
        }
    }
}
