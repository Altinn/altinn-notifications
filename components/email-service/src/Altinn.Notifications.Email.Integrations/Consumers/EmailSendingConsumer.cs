using System.Text.Json;

using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Consumers;

/// <summary>
/// Kafka consumer class for handling the email queue.
/// </summary>
public sealed class EmailSendingConsumer : BackgroundService
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailSendingConsumer> _logger;

    private readonly IConsumer<string, string> _consumer;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendingConsumer"/> class.
    /// </summary>
    public EmailSendingConsumer(
        KafkaSettings kafkaSettings,
        IEmailService emailService,
        ILogger<EmailSendingConsumer> logger)
    {
        _kafkaSettings = kafkaSettings;
        _emailService = emailService;
        _logger = logger;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BrokerAddress,
            GroupId = _kafkaSettings.EmailSendingConsumerSettings.ConsumerGroupId,
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
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeEmail(stoppingToken), stoppingToken);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();

        base.Dispose();
    }

    private async Task ConsumeEmail(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_kafkaSettings.EmailSendingConsumerSettings.TopicName);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult != null)
                {
                    string serializedEmail = consumeResult.Message.Value;
                    Core.Models.Email? email = JsonSerializer.Deserialize<Core.Models.Email>(serializedEmail);
                    await _emailService.SendEmail(email);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while consuming messages");
            throw;
        }
    }
}