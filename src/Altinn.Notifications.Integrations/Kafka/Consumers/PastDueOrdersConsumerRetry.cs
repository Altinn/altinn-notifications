using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for past due orders, first retry
/// </summary>
public class PastDueOrdersConsumerRetry : IHostedService, IDisposable
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILogger<PastDueOrdersConsumerRetry> _logger;
    private readonly KafkaSettings _settings;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IConsumer<string, string> _consumer;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersConsumerRetry"/> class.
    /// </summary>
    public PastDueOrdersConsumerRetry(
        IOrderProcessingService orderProcessingService,
        IOptions<KafkaSettings> settings,
        ILogger<PastDueOrdersConsumerRetry> logger)
    {
        _orderProcessingService = orderProcessingService;
        _settings = settings.Value;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BrokerAddress,
            GroupId = _settings.ConsumerGroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
        .SetErrorHandler((_, e) => _logger.LogError("// PastDueOrdersConsumerRetry // Error: {reason}", e.Reason))
        .Build();
    }

    /// <inheritdoc/>    
    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_settings.PastDueOrdersTopicNameRetry);

        Task.Run(() => ConsumeOrder(_cancellationTokenSource.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();

        return Task.CompletedTask;
    }

    private async Task ConsumeOrder(CancellationToken cancellationToken)
    {
        string message = string.Empty;
        NotificationOrder? order = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult != null)
                {
                    message = consumeResult.Message.Value;
                    bool succeeded = NotificationOrder.TryParse(message, out order);

                    if (!succeeded)
                    {
                        continue;
                    }

                    await _orderProcessingService.ProcessOrderRetry(order!);
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellationToken is canceled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// PastDueOrdersConsumerRetry // ConsumeOrder // An error occurred while consuming messages // OrderId: {orderid}", order == null ? "NotificationOrder is null" : order.Id);
        }
    }
}