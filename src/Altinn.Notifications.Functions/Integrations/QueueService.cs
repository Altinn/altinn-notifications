using Altinn.Notifications.Functions.Configurations;

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public class QueueService : IQueue
    {
        private QueueClient _queueClient;
        private PlatformSettings _settings;
        private ILogger<IQueue> _logger;

        public QueueService(ILogger<IQueue> logger, IOptions<PlatformSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task Push(string message)
        {
            await GetQueueClient();
            await _queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));
            _logger.LogInformation(" // Quueu client // Successfully psuhed to queue.");

        }

        private async Task GetQueueClient()
        {
            if (_queueClient == null)
            {
                _logger.LogInformation(" // Quueu client // Setting up queue client.");

                _queueClient = new QueueClient(_settings.StorageQueueConnectionString, _settings.OutboundQueueName);
                await _queueClient.CreateIfNotExistsAsync();
            }

        }
    }
}
