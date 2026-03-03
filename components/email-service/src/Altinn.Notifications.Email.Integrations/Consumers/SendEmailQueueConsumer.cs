using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Consumers;

/// <summary>
/// Kafka consumer class for handling the email queue.
/// </summary>
public sealed class SendEmailQueueConsumer : KafkaConsumerBase
{
    private readonly string _retryTopicName;
    private readonly ICommonProducer _producer;
    private readonly ISendingService _emailService;
    private readonly ILogger<SendEmailQueueConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendEmailQueueConsumer"/> class.
    /// </summary>
    public SendEmailQueueConsumer(
        KafkaSettings kafkaSettings,
        ISendingService emailService,
        ICommonProducer producer,
        ILogger<SendEmailQueueConsumer> logger)
        : base(kafkaSettings.SendEmailQueueTopicName, kafkaSettings, logger)
    {
        _logger = logger;
        _producer = producer;
        _emailService = emailService;
        _retryTopicName = kafkaSettings.SendEmailQueueRetryTopicName;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ConsumeMessageAsync(ConsumeEmail, RetryEmail, stoppingToken);
    }

    private async Task ConsumeEmail(string message)
    {
        bool succeeded = Core.Sending.Email.TryParse(message, out Core.Sending.Email email);

        if (!succeeded)
        {
            _logger.LogError("// SendEmailQueueConsumer // ConsumeEmail // Deserialization of message failed. {Message}", message);

            return;
        }

        await _emailService.SendAsync(email);
    }

    private async Task RetryEmail(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
