using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Consumers;

/// <summary>
/// Kafka consumer class for handling the email queue.
/// </summary>
public sealed class EmailSendingAcceptedConsumer : KafkaConsumerBase
{
    private readonly IStatusService _statusService;
    private readonly ICommonProducer _producer;
    private readonly string _retryTopicName;
    private const int _processingDelay = 8000;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<EmailSendingAcceptedConsumer> _logger;
    private readonly WolverineSettings _wolverineSettings;
    private readonly IMessageBus? _messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendingAcceptedConsumer"/> class.
    /// </summary>
    public EmailSendingAcceptedConsumer(
        IStatusService statusService,
        ICommonProducer producer,
        KafkaSettings kafkaSettings,
        IDateTimeService dateTime,
        ILogger<EmailSendingAcceptedConsumer> logger,
        WolverineSettings wolverineSettings,
        IMessageBus? messageBus = null)
        : base(kafkaSettings.EmailSendingAcceptedTopicName, kafkaSettings, logger)
    {
        _statusService = statusService;
        _producer = producer;
        _retryTopicName = kafkaSettings.EmailSendingAcceptedTopicName;
        _dateTime = dateTime;
        _logger = logger;
        _wolverineSettings = wolverineSettings;
        _messageBus = messageBus;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ConsumeMessageAsync(ConsumeOperation, RetryOperation, stoppingToken);
    }

    private async Task ConsumeOperation(string message)
    {
        bool succeeded = SendNotificationOperationIdentifier.TryParse(message, out SendNotificationOperationIdentifier operationIdentifier);

        if (!succeeded)
        {
            _logger.LogError("// EmailSendingAcceptedConsumer // ConsumeOperation // Deserialization of message failed. {Message}", message);

            return;
        }

        int diff = (int)(_dateTime.UtcNow() - operationIdentifier.LastStatusCheck).TotalMilliseconds;

        if (diff > 0 && diff < _processingDelay)
        {
            await Task.Delay(_processingDelay - diff);
        }

        if (!_wolverineSettings.EnableCheckEmailSendStatusListener && !_wolverineSettings.EnableCheckEmailSendStatusPublisher)
        {
            await _statusService.UpdateSendStatus(operationIdentifier);
        }
        else if (_messageBus is not null)
        {
            var checkEmailSendStatusCommand = new CheckEmailSendStatusCommand
            {
                LastCheckedAtUtc = _dateTime.UtcNow(),
                SendOperationId = operationIdentifier.OperationId,
                NotificationId = operationIdentifier.NotificationId
            };

            await _messageBus.PublishAsync(checkEmailSendStatusCommand);
        }
    }

    private async Task RetryOperation(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
