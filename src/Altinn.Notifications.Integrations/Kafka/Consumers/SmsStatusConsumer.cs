using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for handling status messages about SMS notifications.
/// Responsible for consuming messages, updating notification status,
/// retrying failed updates, and managing log suppression for repeated failures.
/// </summary>
public class SmsStatusConsumer : KafkaConsumerBase<SmsStatusConsumer>
{
    private readonly string _retryTopicName;
    private readonly IKafkaProducer _producer;
    private readonly IMemoryCache _logSuppressionCache;
    private readonly ILogger<SmsStatusConsumer> _logger;
    private readonly ISmsNotificationService _smsNotificationsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStatusConsumer"/> class.
    /// </summary>
    public SmsStatusConsumer(
        IKafkaProducer producer,
        IMemoryCache memoryCache,
        IOptions<KafkaSettings> settings,
        ILogger<SmsStatusConsumer> logger,
        ISmsNotificationService smsNotificationsService)
        : base(settings, logger, settings.Value.SmsStatusUpdatedTopicName)
    {
        _logger = logger;
        _producer = producer;
        _logSuppressionCache = memoryCache;
        _smsNotificationsService = smsNotificationsService;
        _retryTopicName = settings.Value.SmsStatusUpdatedTopicName;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Processes an SMS status message. If updating the status fails with
    /// <see cref="SendStatusUpdateException"/>, the message will be logged and retried.
    /// </summary>
    /// <param name="message">Raw Kafka message payload.</param>
    private async Task ProcessStatus(string message)
    {
        bool succeeded = SmsSendOperationResult.TryParse(message, out SmsSendOperationResult result);

        if (!succeeded)
        {
            _logger.LogError("// SmsStatusConsumer // ProcessStatus // Deserialization of message failed. {Message}", message);
            return;
        }

        try
        {
            await _smsNotificationsService.UpdateSendStatus(result);
        }
        catch (SendStatusUpdateException e)
        {
            string suppressionKey = e.Identifier ?? result.NotificationId?.ToString() ?? result.GatewayReference ?? "unknown";
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

                _logger.LogInformation(e, "Could not update SMS send status for message: {Message}", message);
            }

            await RetryStatus(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not update SMS send status for message: {Message}", message);
            throw;
        }
    }

    /// <summary>
    /// Republishes a failed status message to the retry Kafka topic.
    /// </summary>
    /// <param name="message">The message payload.</param>
    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
