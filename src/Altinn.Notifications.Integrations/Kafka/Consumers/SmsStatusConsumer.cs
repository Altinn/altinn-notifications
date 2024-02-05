using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for status messages about sms notifications
/// </summary>
public class SmsStatusConsumer : KafkaConsumerBase<SmsStatusConsumer>
{
    private readonly ISmsNotificationService _smsNotificationsService;
    private readonly IKafkaProducer _producer;
    private readonly string _retryTopicName;
    private readonly ILogger<SmsStatusConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStatusConsumer"/> class.
    /// </summary>
    public SmsStatusConsumer(
        ISmsNotificationService smsNotificationsService,
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<SmsStatusConsumer> logger)
        : base(settings, logger, settings.Value.SmsStatusUpdatedTopicName)
    {
        _smsNotificationsService = smsNotificationsService;
        _producer = producer;
        _retryTopicName = settings.Value.SmsStatusUpdatedTopicName;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    private async Task ProcessStatus(string message)
    {
        bool succeeded = SmsSendOperationResult.TryParse(message, out SmsSendOperationResult result);

        if (!succeeded)
        {
            _logger.LogError("// SmsStatusConsumer // ProcessStatus // Deserialization of message failed. {Message}", message);
            return;
        }

        await _smsNotificationsService.UpdateSendStatus(result);
    }

    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message!);
    }
}
