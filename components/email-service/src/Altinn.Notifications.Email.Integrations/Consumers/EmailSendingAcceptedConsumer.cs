using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Consumers;

/// <summary>
/// Kafka consumer class for handling the email queue.
/// </summary>
public sealed class EmailSendingAcceptedConsumer : KafkaConsumerBase
{
    private readonly IStatusService _statusService;
    private readonly ICommonProducer _producer;
    private readonly string _retryTopicName;
    private const int _processingDelay = 8000;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<EmailSendingAcceptedConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendingAcceptedConsumer"/> class.
    /// </summary>
    public EmailSendingAcceptedConsumer(
        IStatusService statusService,
        ICommonProducer producer,
        KafkaSettings kafkaSettings,
        IDateTimeService dateTime,
        ILogger<EmailSendingAcceptedConsumer> logger)
        : base(kafkaSettings, logger, kafkaSettings.EmailSendingAcceptedTopicName)
    {
        _statusService = statusService;
        _producer = producer;
        _retryTopicName = kafkaSettings.EmailSendingAcceptedTopicName;
        _dateTime = dateTime;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ConsumeOperation, RetryOperation, stoppingToken), stoppingToken);
    }

    private async Task ConsumeOperation(string message)
    {
        bool succeeded = SendNotificationOperationIdentifier.TryParse(message, out SendNotificationOperationIdentifier operationIdentifier);

        if (!succeeded)
        {
            _logger.LogError("// EmailSendingAcceptedConsumer // ConsumeOperation // Deserialization of message failed. {Message}", message);

            return;
        }

        int diff = (int)(_dateTime.UtcNow() - operationIdentifier.LastStatusCheck).TotalMilliseconds;

        if (diff > 0 && diff < _processingDelay)
        {
            await Task.Delay(_processingDelay - diff);
        }

        await _statusService.UpdateSendStatus(operationIdentifier);
    }

    private async Task RetryOperation(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
