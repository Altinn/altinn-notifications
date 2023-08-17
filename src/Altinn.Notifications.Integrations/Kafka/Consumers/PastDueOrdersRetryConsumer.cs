using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for past due orders, first retry
/// </summary>
public class PastDueOrdersRetryConsumer : KafkaConsumerBase<PastDueOrdersRetryConsumer>
{
    private readonly IOrderProcessingService _orderProcessingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersRetryConsumer"/> class.
    /// </summary>
    public PastDueOrdersRetryConsumer(
        IOrderProcessingService orderProcessingService,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersRetryConsumer> logger)
        : base(settings, logger, settings.Value.PastDueOrdersRetryTopicName)
    {
        _orderProcessingService = orderProcessingService;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeOrder(ProcessOrder, RetryOrder, stoppingToken), stoppingToken);
    }

    private async Task ProcessOrder(NotificationOrder order)
    {
        await _orderProcessingService.ProcessOrderRetry(order!);
    }

    private Task RetryOrder(string message)
    {
        return Task.CompletedTask;
    }
}