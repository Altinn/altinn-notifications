using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;

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
    /// <param name="producer">
    /// The Kafka producer responsible for publishing the status‑check message.
    /// </param>
    /// <param name="dateTime">
    /// Supplies the current UTC timestamp applied to the message as the initial status‑check time.
    /// </param>
    /// <param name="topicName">
    /// The name of the Kafka topic where the status‑check message will be published.
    /// </param>
    public EmailStatusCheckProducer(ICommonProducer producer, IDateTimeService dateTime, string topicName)
    {
        _producer = producer;
        _dateTime = dateTime;
        _topicName = topicName;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(Guid notificationId, string operationId)
    {
        var identifier = new SendNotificationOperationIdentifier
        {
            OperationId = operationId,
            NotificationId = notificationId,
            LastStatusCheck = _dateTime.UtcNow()
        };

        await _producer.ProduceAsync(_topicName, identifier.Serialize());
    }
}
