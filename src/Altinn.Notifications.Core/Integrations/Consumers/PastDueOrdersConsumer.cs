using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Integrations.Consumers;

/// <summary>
/// Kafka Consumer class for past due orders
/// </summary>
public class PastDueOrdersConsumer : IHostedService
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILogger<PastDueOrdersConsumer> _logger;
    private readonly KafkaSettings _settings;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IConsumer<string, string> _consumer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersConsumer"/> class.
    /// </summary>
    public PastDueOrdersConsumer(IOrderProcessingService orderProcessingService, IOptions<KafkaSettings> settings, ILogger<PastDueOrdersConsumer> logger)
    {
        _orderProcessingService = orderProcessingService;
        _logger = logger;
        _settings = settings.Value;
        _cancellationTokenSource = new CancellationTokenSource();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BrokerAddress,
            GroupId = _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
        .SetErrorHandler((_, e) => _logger.LogError("Error: {reason}", e.Reason))
        .SetStatisticsHandler((_, json) => _logger.LogInformation("Statistics: {json}", json))
        .SetPartitionsAssignedHandler((c, partitions) =>
        {
            _logger.LogInformation("Assigned partitions: [partitions]", string.Join(", ", partitions));
        })
        .SetPartitionsRevokedHandler((c, partitions) =>
        {
            _logger.LogInformation("Revoking assignment for partitions: [partitions]", string.Join(", ", partitions));
        })
        .Build();
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_settings.PastDueOrdersTopicName);

        Task.Run(() => ConsumeOrder(_cancellationTokenSource.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();

        _consumer.Close();
        _consumer.Dispose();

        return Task.CompletedTask;
    }

    private async Task ConsumeOrder(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult != null)
                {
                    var order = NotificationOrder.Deserialize(consumeResult.Message.Value);

                    await _orderProcessingService.ProcessOrder(order!);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellationToken is canceled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while consuming messages");
            throw;
        }
    }
}