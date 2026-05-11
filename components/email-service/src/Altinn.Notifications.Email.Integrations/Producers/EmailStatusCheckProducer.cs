using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Configuration;

namespace Altinn.Notifications.Email.Integrations.Producers;

/// <summary>
/// Kafka-based implementation of <see cref="IEmailStatusCheckDispatcher"/> that publishes a
/// <see cref="SendNotificationOperationIdentifier"/> message to a configured topic.
/// This message initiates status tracking for an email send operation handled by Azure Communication Services (ACS).
/// This implementation is used when the Azure Service Bus transport is disabled (legacy integration path).
/// </summary>
public class EmailStatusCheckProducer : IEmailStatusCheckDispatcher
{
    private readonly string _topicName;
    private readonly ICommonProducer _producer;
    private readonly IDateTimeService _dateTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusCheckProducer"/> class.
    /// </summary>
    public EmailStatusCheckProducer(ICommonProducer producer, IDateTimeService dateTime, KafkaSettings settings)
    {
        _producer = producer;
        _dateTime = dateTime;
        _topicName = settings.EmailSendingAcceptedTopicName;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(Guid notificationId, string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentOutOfRangeException.ThrowIfEqual(notificationId, Guid.Empty);

        var identifier = new SendNotificationOperationIdentifier
        {
            OperationId = operationId,
            NotificationId = notificationId,
            LastStatusCheck = _dateTime.UtcNow()
        };

        bool success = await _producer.ProduceAsync(_topicName, identifier.Serialize());
        if (!success)
        {
            throw new InvalidOperationException("Failed to publish email status-check message.");
        }
    }
}
