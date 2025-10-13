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
    private readonly string _statusUpdateTopicName;
    private readonly string _statusRetryUpdateTopicName;
    private readonly IKafkaProducer _producer;
    private readonly ILogger<TConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStatusConsumerBase{TConsumer, TResult}"/> class.
    /// </summary>
    protected NotificationStatusConsumerBase(
        string topicName,
        string retryTopicName,
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<TConsumer> logger)
        : base(settings, logger, topicName)
    {
        _logger = logger;
        _producer = producer;
        _statusUpdateTopicName = topicName;
        _statusRetryUpdateTopicName = retryTopicName;
    }

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
            await HandleSendStatusUpdateException(message, e);
        }
        catch (ArgumentException) when (LogProcessingError(message))
        {
            throw;
        }
        catch (InvalidOperationException) when (LogProcessingError(message))
        {
            throw;
        }
    }

    private async Task HandleSendStatusUpdateException(string message, SendStatusUpdateException e)
    {
        Guid? notificationId = null;
        string? externalReferenceId = null;

        if (e.IdentifierType == SendStatusIdentifierType.NotificationId && Guid.TryParse(e.Identifier, out var parsedNoticiationId))
        {
            notificationId = parsedNoticiationId;
        }

        if (e.IdentifierType == SendStatusIdentifierType.OperationId || e.IdentifierType == SendStatusIdentifierType.GatewayReference)
        {
            externalReferenceId = e.Identifier;
        }

        var retryMessage = new UpdateStatusRetryMessage
        {
            Attempts = 1,
            SendResult = message,
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            NotificationId = notificationId,
            ExternalReferenceId = externalReferenceId
        };

        var serializedRetryMessage = retryMessage.Serialize();

        await _producer.ProduceAsync(_statusRetryUpdateTopicName, serializedRetryMessage);
    }

    /// <summary>
    /// Sends a message to the retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_statusUpdateTopicName, message);
    }

    /// <summary>
    /// Logs an error when an exception occurs while processing a Kafka message.
    /// </summary>
    /// <param name="kafkaMessage">The Kafka message that caused the error.</param>
    /// <returns>Always returns <c>false</c> to allow the exception to propagate.</returns>
    private bool LogProcessingError(string kafkaMessage)
    {
        _logger.LogError("// {Consumer} // ProcessStatus // Deserialization of message failed due to malformed JSON. Not retrying. {Message}", typeof(TConsumer).Name, kafkaMessage);
        return true;
    }
}
