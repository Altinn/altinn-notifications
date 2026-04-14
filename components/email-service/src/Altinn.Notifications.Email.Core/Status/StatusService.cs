using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core;

/// <summary>
/// A service implementation of the <see cref="IStatusService"/> class
/// </summary>
public class StatusService : IStatusService
{
    private readonly TopicSettings _settings;
    private readonly ICommonProducer _producer;
    private readonly IDateTimeService _dateTime;
    private readonly IEmailServiceClient _emailServiceClient;
    private readonly IEmailSendResultDispatcher _emailSendResultDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusService"/> class.
    /// </summary>
    public StatusService(
        TopicSettings settings,
        ICommonProducer producer,
        IDateTimeService dateTime,
        IEmailServiceClient emailServiceClient,
        IEmailSendResultDispatcher emailSendResultDispatcher)
    {
        _settings = settings;
        _producer = producer;
        _dateTime = dateTime;
        _emailServiceClient = emailServiceClient;
        _emailSendResultDispatcher = emailSendResultDispatcher;
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SendNotificationOperationIdentifier operationIdentifier)
    {
        EmailSendResult result = await _emailServiceClient.GetOperationUpdate(operationIdentifier.OperationId);

        if (result != EmailSendResult.Sending)
        {
            var operationResult = new SendOperationResult()
            {
                SendResult = result,
                OperationId = operationIdentifier.OperationId,
                NotificationId = operationIdentifier.NotificationId
            };

            await _emailSendResultDispatcher.DispatchAsync(operationResult);
        }
        else
        {
            operationIdentifier.LastStatusCheck = _dateTime.UtcNow();
            await _producer.ProduceAsync(_settings.EmailSendingAcceptedTopicName, operationIdentifier.Serialize());
        }
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SendOperationResult sendOperationResult)
    {
        await _emailSendResultDispatcher.DispatchAsync(sendOperationResult);
    }
}
