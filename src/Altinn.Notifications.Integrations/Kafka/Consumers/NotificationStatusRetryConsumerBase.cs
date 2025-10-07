using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Base class for notification status retry consumers that handle time-based retry logic and dead delivery reports
/// </summary>
/// <typeparam name="TConsumer">The type of the derived consumer</typeparam>
public abstract class NotificationStatusRetryConsumerBase<TConsumer> : KafkaConsumerBase<TConsumer>
    where TConsumer : class
{
    private readonly IKafkaProducer _producer;
    private readonly IDeadDeliveryReportService _deadDeliveryReportService;
    private readonly ILogger<TConsumer> _logger;
    private readonly string _retryTopicName;
    private readonly int _statusUpdateRetrySeconds;

    /// <summary>
    /// Gets the delivery report channel for this consumer type
    /// </summary>
    protected abstract DeliveryReportChannel Channel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStatusRetryConsumerBase{TConsumer}"/> class.
    /// </summary>
    /// <param name="producer">The Kafka producer used for publishing retry messages.</param>
    /// <param name="deadDeliveryReportService">Service for handling dead delivery reports.</param>
    /// <param name="settings">Kafka configuration settings.</param>
    /// <param name="logger">Logger for the consumer.</param>
    /// <param name="retryTopicName">The name of the retry topic to consume from and publish to.</param>
    protected NotificationStatusRetryConsumerBase(
        IKafkaProducer producer,
        IDeadDeliveryReportService deadDeliveryReportService,
        IOptions<Configuration.KafkaSettings> settings,
        ILogger<TConsumer> logger,
        string retryTopicName)
        : base(settings, logger, retryTopicName)
    {
        _producer = producer;
        _deadDeliveryReportService = deadDeliveryReportService;
        _logger = logger;
        _retryTopicName = retryTopicName;
        _statusUpdateRetrySeconds = settings.Value.StatusUpdatedRetryThresholdSeconds;
    }

    /// <summary>
    /// Executes the retry consumer to process messages from the Kafka topic
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Processes a status retry message, either creating a dead delivery report or re-queuing for retry
    /// </summary>
    /// <param name="message">The raw message containing the UpdateStatusRetryMessage</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task ProcessStatus(string message)
    {
        var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message) ?? throw new InvalidOperationException("Could not deserialize message");

        var elapsedSeconds = (DateTime.UtcNow - retryMessage.FirstSeen).TotalSeconds;

        if (elapsedSeconds >= _statusUpdateRetrySeconds)
        {
            _logger.LogInformation("Processing retry message after {ElapsedSeconds} seconds", elapsedSeconds);

            // Persist the dead delivery report after hitting the retry threshold
            var deadDeliveryReport = new DeadDeliveryReport
            {
                AttemptCount = retryMessage.Attempts,
                Channel = Channel,
                FirstSeen = retryMessage.FirstSeen,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                DeliveryReport = retryMessage.SendResult ?? string.Empty
            };

            await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
        }
        else
        {
            // increment retries before putting it back on the retry topic
            var incrementedRetryMessage = retryMessage with { Attempts = retryMessage.Attempts + 1 };

            _logger.LogDebug(
                "Message not ready for retry. Elapsed: {ElapsedSeconds}s, Threshold: {ThresholdSeconds}s",
                elapsedSeconds,
                _statusUpdateRetrySeconds);

            await _producer.ProduceAsync(_retryTopicName, incrementedRetryMessage.Serialize());
        }
    }

    /// <summary>
    /// Sends a message to the retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
