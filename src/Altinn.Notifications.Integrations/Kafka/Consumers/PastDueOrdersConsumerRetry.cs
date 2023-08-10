using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for past due orders, first retry
/// </summary>
public class PastDueOrdersConsumerRetry : KafkaConsumerBase<PastDueOrdersConsumerRetry>
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersConsumerRetry"/> class.
    /// </summary>
    public PastDueOrdersConsumerRetry(
        IOrderProcessingService orderProcessingService,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersConsumerRetry> logger)
        : base(settings, logger, settings.Value.PastDueOrdersTopicNameRetry)
    {
        _orderProcessingService = orderProcessingService;
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
        await _orderProcessingService.ProcessOrderRetry(order);
    }

    private Task RetryOrder(string message)
    {
        return Task.CompletedTask;
    }
}