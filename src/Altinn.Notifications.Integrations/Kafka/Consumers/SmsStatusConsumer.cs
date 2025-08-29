using System.Collections.Concurrent;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

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
    private readonly ILogger<SmsStatusConsumer> _logger;
    private readonly ISmsNotificationService _smsNotificationsService;

    private readonly ConcurrentQueue<string> _cleanupCandidates = new();
    private readonly TimeSpan _logSuppressionDuration = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, DateTime> _loggedMessagesCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStatusConsumer"/> class.
    /// </summary>
    public SmsStatusConsumer(
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<SmsStatusConsumer> logger,
        ISmsNotificationService smsNotificationsService)
        : base(settings, logger, settings.Value.SmsStatusUpdatedTopicName)
    {
        _logger = logger;
        _producer = producer;
        _smsNotificationsService = smsNotificationsService;
        _retryTopicName = settings.Value.SmsStatusUpdatedTopicName;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupTask = CleanLoggedMessagesCache(stoppingToken);
        var consumeTask = ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken);

        return Task.WhenAll(consumeTask, cleanupTask);
    }

    /// <summary>
    /// Background loop that removes entries from the logged-messages cache that are older than 15 seconds.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token tied to service shutdown.</param>
    private async Task CleanLoggedMessagesCache(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                DateTime cutoff = DateTime.UtcNow - _logSuppressionDuration;
                while (_cleanupCandidates.TryDequeue(out var key))
                {
                    if (_loggedMessagesCache.TryGetValue(key, out var lastLogged))
                    {
                        if (lastLogged < cutoff)
                        {
                            _loggedMessagesCache.TryRemove(key, out _);
                        }
                        else
                        {
                            _cleanupCandidates.Enqueue(key);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <summary>
    /// Processes a SMS status message. If updating the status fails with
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
            bool shouldLog = false;
            DateTime dateTimeNow = DateTime.UtcNow;

            DateTime OnFirstSeen()
            {
                shouldLog = true;
                _cleanupCandidates.Enqueue(e.Identifier);
                return dateTimeNow;
            }

            _loggedMessagesCache.AddOrUpdate(
                e.Identifier,
                _ => OnFirstSeen(),
                (_, lastLogged) =>
                {
                    if ((dateTimeNow - lastLogged) > _logSuppressionDuration)
                    {
                        shouldLog = true;
                    }

                    return dateTimeNow;
                });

            if (shouldLog)
            {
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
