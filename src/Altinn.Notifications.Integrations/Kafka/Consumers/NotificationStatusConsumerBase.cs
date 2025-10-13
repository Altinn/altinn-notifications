using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Base class for notification status consumers handling deserialize, status update, and selective retry.
/// </summary>
/// <typeparam name="TConsumer">The type of the consumer.</typeparam>
/// <typeparam name="TResult">The type of the result after deserializing the message.</typeparam>
public abstract class NotificationStatusConsumerBase<TConsumer, TResult> : KafkaConsumerBase<TConsumer>
    where TResult : class
    where TConsumer : class
{
    private readonly string _retryTopicName;
    private readonly IKafkaProducer _producer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStatusConsumerBase{TConsumer, TResult}"/> class.
    /// </summary>
    /// <param name="topicName">The name of the Kafka topic to consume from.</param>
    /// <param name="retryTopicName">The name of the Kafka topic to publish retry messages to.</param>
    /// <param name="producer">The Kafka producer used for publishing retry messages.</param>
    /// <param name="settings">Kafka configuration settings.</param>
    /// <param name="logger">Logger for the consumer.</param>
    protected NotificationStatusConsumerBase(
        string topicName,
        string retryTopicName,
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger logger)
        : base(settings, logger, topicName)
    {
        _logger = logger;
        _producer = producer;
        _retryTopicName = retryTopicName;
    }

    /// <summary>
    /// Gets the name of the notification channel being processed (e.g. SMS or Email).
    /// </summary>
    protected abstract string ChannelName { get; }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Updates the notification status based on the parsed result.
    /// </summary>
    /// <param name="result">The parsed result containing status update information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected abstract Task UpdateStatusAsync(TResult result);

    /// <summary>
    /// Attempts to parse a message into the result type.
    /// </summary>
    /// <param name="message">The message to parse.</param>
    /// <param name="result">The parsed result if successful; otherwise, null.</param>
    /// <returns><c>true</c> if parsing was successful; otherwise, <c>false</c>.</returns>
    protected abstract bool TryParse(string message, out TResult result);

    /// <summary>
    /// Processes a delivery report message received from Kafka.
    /// </summary>
    /// <param name="message">The raw message to process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessStatus(string message)
    {
        if (!TryParse(message, out TResult result))
        {
            _logger.LogError("// {Consumer} // ProcessStatus // Deserialization of message failed. {Message}", typeof(TConsumer).Name, message);
            return;
        }

        try
        {
            await UpdateStatusAsync(result);
        }
        catch (SendStatusUpdateException e)
        {
            Guid? notificationId = null;
            Guid? externalReferenceId = null;

            if (e.IdentifierType == SendStatusIdentifierType.NotificationId && Guid.TryParse(e.Identifier, out var parsedNoticiationId))
            {
                notificationId = parsedNoticiationId;
            }

            if (e.IdentifierType == SendStatusIdentifierType.OperationId && Guid.TryParse(e.Identifier, out var parsedExternalReferenceId))
            {
                externalReferenceId = parsedExternalReferenceId;
            }

            var retryMessage = new UpdateStatusRetryMessage
            {
                FirstSeen = DateTime.UtcNow,
                Attempts = 1,
                LastAttempt = DateTime.UtcNow,
                NotificationId = notificationId,
                ExternalReferenceId = externalReferenceId,
                SendResult = message
            };

            var serializedRetryMessage = retryMessage.Serialize();

            await RetryStatus(serializedRetryMessage);
        }
        catch (Exception e) when (LogProcessingError(e, message))
        {
            throw;
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

    /// <summary>
    /// Logs an error when an exception occurs while processing a Kafka message.
    /// </summary>
    /// <param name="exception">The exception that occurred during processing.</param>
    /// <param name="kafkaMessage">The Kafka message that caused the error.</param>
    /// <returns>Always returns <c>false</c> to allow the exception to propagate.</returns>
    private bool LogProcessingError(Exception exception, string kafkaMessage)
    {
        _logger.LogError(
            exception,
            "Could not update {Channel} send status for message: {Message}",
            ChannelName,
            kafkaMessage);

        return false;
    }
}
