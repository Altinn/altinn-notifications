using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for status messages about email notifications
/// </summary>
public class EmailStatusConsumer : KafkaConsumerBase<EmailStatusConsumer>
{
    private readonly IEmailNotificationService _emailNotificationsService;
    private readonly IKafkaProducer _producer;
    private readonly string _retryTopicName;
    private readonly ILogger<EmailStatusConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusConsumer"/> class.
    /// </summary>
    public EmailStatusConsumer(
        IEmailNotificationService emailNotificationsService,
        IKafkaProducer producer, 
        IOptions<KafkaSettings> settings, 
        ILogger<EmailStatusConsumer> logger)
        : base(settings, logger, settings.Value.EmailStatusUpdatedTopicName)
    {
        _emailNotificationsService = emailNotificationsService;
        _producer = producer;
        _retryTopicName = settings.Value.EmailStatusUpdatedTopicName;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    private async Task ProcessStatus(string message)
    {
        bool succeeded = SendOperationResult.TryParse(message, out SendOperationResult result);

        if (!succeeded)
        {
            _logger.LogError("// EmailStatusConsumer // ProcessStatus // Deserialization of message failed. {Message}", message);
            return;
        }

        await _emailNotificationsService.UpdateSendStatus(result);
    }

    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message!);
    }
}
