using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for status messages about email notifications
/// </summary>
public class EmailStatusConsumer : KafkaConsumerBase<EmailStatusConsumer>
{
    private readonly string _retryTopicName;
    private readonly IKafkaProducer _producer;
    private readonly IMemoryCache _logSuppressionCache;
    private readonly ILogger<EmailStatusConsumer> _logger;
    private readonly IEmailNotificationService _emailNotificationsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusConsumer"/> class.
    /// </summary>
    public EmailStatusConsumer(
        IKafkaProducer producer,
        IMemoryCache memoryCache,
        IOptions<KafkaSettings> settings,
        ILogger<EmailStatusConsumer> logger,
        IEmailNotificationService emailNotificationsService)
        : base(settings, logger, settings.Value.EmailStatusUpdatedTopicName)
    {
        _logger = logger;
        _producer = producer;
        _logSuppressionCache = memoryCache;
        _emailNotificationsService = emailNotificationsService;
        _retryTopicName = settings.Value.EmailStatusUpdatedTopicName;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    private async Task ProcessStatus(string message)
    {
        bool succeeded = EmailSendOperationResult.TryParse(message, out EmailSendOperationResult result);

        if (!succeeded)
        {
            _logger.LogError("// EmailStatusConsumer // ProcessStatus // Deserialization of message failed. {Message}", message);
            return;
        }

        try
        {
            await _emailNotificationsService.UpdateSendStatus(result);
        }
        catch (KeyNotFoundException e)
        {
            string suppressionKey = result.OperationId ?? result.NotificationId?.ToString() ?? "unknown";
            bool shouldBeLogged = !_logSuppressionCache.TryGetValue(suppressionKey, out _);

            if (shouldBeLogged)
            {
                _logSuppressionCache.Set(
                    suppressionKey,
                    true,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                    });

                _logger.LogInformation(e, "Could not update email send status for message: {Message}", message);
            }

            await RetryStatus(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not update email send status for message: {Message}", message);
            throw;
        }
    }

    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
