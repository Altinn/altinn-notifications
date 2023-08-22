using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for past due orders
/// </summary>
public class PastDueOrdersConsumer : KafkaConsumerBase<PastDueOrdersConsumer>
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IKafkaProducer _producer;
    private readonly string _retryTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersConsumer"/> class.
    /// </summary>
    public PastDueOrdersConsumer(
        IOrderProcessingService orderProcessingService,
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersConsumer> logger)
        : base(settings, logger, settings.Value.PastDueOrdersTopicName)
    {
        _orderProcessingService = orderProcessingService;
        _producer = producer;
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

        await _orderProcessingService.ProcessOrder(order);
    }

    private async Task RetryOrder(string message)
    {
        await _producer.ProduceAsync(_retryTopic, message!);
    }
}