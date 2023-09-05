using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core;

/// <summary>
/// A service implementation of the <see cref="IStatusService"/> class
/// </summary>
public class StatusService : IStatusService
{
    private readonly IEmailServiceClient _emailServiceClient;
    private readonly TopicSettings _settings;
    private readonly ICommonProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusService"/> class.
    /// </summary>
    /// <param name="emailServiceClient">A client that can perform actual mail sending.</param>
    /// <param name="producer">A kafka producer.</param>
    /// <param name="settings">The topic settings.</param>
    public StatusService(
        IEmailServiceClient emailServiceClient,
        ICommonProducer producer,
        TopicSettings settings)
    {
        _emailServiceClient = emailServiceClient;
        _producer = producer;
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SendNotificationOperationIdentifier operationIdentifier)
    {
        EmailSendResult result = await _emailServiceClient.GetOperationUpdate(operationIdentifier.OperationId);

        if (result != EmailSendResult.Sending)
        {
            var operationResult = new SendOperationResult()
            {
                NotificationId = operationIdentifier.NotificationId,
                OperationId = operationIdentifier.OperationId,
                SendResult = result
            };

            await _producer.ProduceAsync(_settings.EmailStatusUpdatedTopicName, operationResult.Serialize());
        }
        else
        {
            await _producer.ProduceAsync(_settings.EmailSendingAcceptedRetryTopicName, operationIdentifier.Serialize());
        }
    }
}
