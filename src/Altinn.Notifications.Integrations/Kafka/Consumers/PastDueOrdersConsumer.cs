using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for past due orders
/// </summary>
public class PastDueOrdersConsumer : KafkaConsumerBase
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IKafkaProducer _producer;
    private readonly string _retryTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersConsumer"/> class.
    /// </summary>
    public PastDueOrdersConsumer(
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersConsumer> logger,
        IOrderProcessingService orderProcessingService) : base(settings, logger, settings.Value.PastDueOrdersTopicName)
    {
        _producer = producer;
        _orderProcessingService = orderProcessingService;
        _retryTopic = settings.Value.PastDueOrdersRetryTopicName;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessOrder, RetryOrder, stoppingToken), stoppingToken);
    }

    private async Task ProcessOrder(string message)
    {
        bool succeeded = NotificationOrder.TryParse(message, out NotificationOrder order);

        if (!succeeded)
        {
            return;
        }

        var processingResult = await _orderProcessingService.ProcessOrder(order);
        if (processingResult.IsRetryRequired)
        {
            await _producer.ProduceAsync(_retryTopic, message);
        }
    }

    private async Task RetryOrder(string message)
    {
        await _producer.ProduceAsync(_retryTopic, message!);
    }
}
