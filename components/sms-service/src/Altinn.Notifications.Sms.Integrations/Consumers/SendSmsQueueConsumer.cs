using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.Consumers;

/// <summary>
/// Kafka consumer class for handling the sms queue.
/// </summary>
public sealed class SendSmsQueueConsumer : KafkaConsumerBase
{
    private readonly ISendingService _sendingService;
    private readonly ICommonProducer _producer;
    private readonly ILogger<SendSmsQueueConsumer> _logger;
    private readonly string _retryTopicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendSmsQueueConsumer"/> class.
    /// </summary>
    public SendSmsQueueConsumer(
        KafkaSettings kafkaSettings,
        ISendingService sendingService,
        ICommonProducer producer,
        ILogger<SendSmsQueueConsumer> logger)
        : base(kafkaSettings, logger, kafkaSettings.SendSmsQueueTopicName)
    {
        _sendingService = sendingService;
        _producer = producer;
        _retryTopicName = kafkaSettings.SendSmsQueueRetryTopicName;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ConsumeSms, RetrySms, stoppingToken), stoppingToken);
    }

    private async Task ConsumeSms(string message)
    {
        bool succeeded = Core.Sending.Sms.TryParse(message, out Core.Sending.Sms sms);

        if (!succeeded)
        {
            _logger.LogError("// SendSmsQueueConsumer // ConsumeSms // Deserialization of message failed. {Message}", message);

            return;
        }

        await _sendingService.SendAsync(sms);
    }

    private async Task RetrySms(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
