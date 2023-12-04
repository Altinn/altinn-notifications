using Altinn.Notifications.Core.Models.AltinnServiceUpdate;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers
{
    /// <summary>
    /// Kafka consumer class for Altinn service updates
    /// </summary>
    public class AltinnServiceUpdateConsumer : KafkaConsumerBase<AltinnServiceUpdateConsumer>
    {
        private readonly IAltinnServiceUpdateService _serviceUpdate;
        private readonly ILogger<AltinnServiceUpdateConsumer> _logger;

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
            _logger = logger;
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
                _logger.LogError("// AltinnServiceUpdateConsumer // ProcessServiceUpdate // Deserialization of message failed. {Message}", message);
                return;
            }

            await _serviceUpdate.HandleServiceUpdate(update.Source.ToLower().Trim(), update.Schema, update.Data);
        }

        private async Task RetryServiceUpdate(string message)
        {
            // Making a second attempt, but no further action if it fails again.
            await ProcessServiceUpdate(message);
        }
    }
}
