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
    private readonly string _retryTopic;
    private readonly CancellationTokenSource _cancellationTokenSource;

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
        _retryTopic = settings.Value.PastDueOrdersTopicNameRetry;
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task.Run(() => ConsumeOrder(ProcessOrder, RetryOrder, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    private async Task ProcessOrder(NotificationOrder order)
    {
        await _orderProcessingService.ProcessOrder(order);
    }

    private async Task RetryOrder(string message)
    {
        await _producer.ProduceAsync(_retryTopic, message!);
    }
}