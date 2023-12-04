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
        Result<string, EmailClientErrorResponse> result = await _emailServiceClient.SendEmail(email);

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
            async emailSendFailResponse =>
            {
                if (emailSendFailResponse.SendResult == EmailSendResult.Failed_TransientError)
                {
                    ResourceLimitExceeded resourceLimitExceeded = new ResourceLimitExceeded()
                    {
                        Resource = "azure-communication-services-email",
                        ResetTime = DateTime.UtcNow.AddSeconds((double)emailSendFailResponse.IntermittentErrorDelay!)
                    };

                    GenericServiceUpdate genericServiceUpdate = new()
                    {
                        Source = "platform-notifications-email",
                        Schema = AltinnServiceUpdateSchema.ResourceLimitExceeded,
                        Data = resourceLimitExceeded.Serialize()
                    };

                    await _producer.ProduceAsync(_settings.AltinnServiceUpdateTopicName, genericServiceUpdate.Serialize());
                }

                var operationResult = new SendOperationResult()
                {
                    NotificationId = email.NotificationId,
                    SendResult = emailSendFailResponse.SendResult
                };

                await _producer.ProduceAsync(_settings.EmailStatusUpdatedTopicName, operationResult.Serialize());
            });
    }
}
