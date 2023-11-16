using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Service responsible for handling email sending requests.
/// </summary>
public class SendingService : ISendingService
{
    private readonly IEmailServiceClient _emailServiceClient;
    private readonly TopicSettings _settings;
    private readonly ICommonProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendingService"/> class.
    /// </summary>
    /// <param name="emailServiceClient">A client that can perform actual mail sending.</param>
    /// <param name="producer">A kafka producer.</param>
    /// <param name="settings">The topic settings.</param>
    public SendingService(
        IEmailServiceClient emailServiceClient,
        ICommonProducer producer,
        TopicSettings settings)
    {
        _emailServiceClient = emailServiceClient;
        _producer = producer;
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task SendAsync(Email email)
    {
        Result<string, EmailSendResult> result = await _emailServiceClient.SendEmail(email);

        await result.Match(
            async operationId =>
            {
                var operationIdentifier = new SendNotificationOperationIdentifier()
                {
                    NotificationId = email.NotificationId,
                    OperationId = operationId
                };

                await _producer.ProduceAsync(_settings.EmailSendingAcceptedTopicName, operationIdentifier.Serialize());
            },
            async emailSendResult =>
            {
                var operationResult = new SendOperationResult()
                {
                    NotificationId = email.NotificationId,
                    SendResult = emailSendResult
                };

                await _producer.ProduceAsync(_settings.EmailStatusUpdatedTopicName, operationResult.Serialize());
            });
    }
}
