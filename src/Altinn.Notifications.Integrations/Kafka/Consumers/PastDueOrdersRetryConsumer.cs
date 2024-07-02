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
    private readonly IDateTimeService _dateTime;

    private readonly int _processingDelay = 60000;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersRetryConsumer"/> class.
    /// </summary>
    public PastDueOrdersRetryConsumer(
        IOrderProcessingService orderProcessingService,
        IDateTimeService dateTimeService,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersRetryConsumer> logger)
        : base(settings, logger, settings.Value.PastDueOrdersRetryTopicName)
    {
        _orderProcessingService = orderProcessingService;
        _dateTime = dateTimeService;
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

        // adding a delay relative to send time to allow transient faults to be resolved
        int diff = (int)(_dateTime.UtcNow() - order.RequestedSendTime).TotalMilliseconds;

        if (diff < _processingDelay)
        {
            await Task.Delay(_processingDelay - diff);
        }

        await _orderProcessingService.ProcessOrderRetry(order!);
    }

    private Task RetryOrder(string message)
    {
        return Task.CompletedTask;
    }
}
