using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

using Microsoft.Extensions.Logging;

using Wolverine;
using Wolverine.Attributes;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="CheckEmailSendStatusCommand"/> by polling ACS for email delivery status.
/// </summary>
[WolverineHandler]
public static class CheckEmailSendStatusHandler
{
    private const int _statusPollDelayMs = 8000;

    /// <summary>
    /// Polls ACS for delivery status. If terminal, publishes the result to Kafka.
    /// If still sending, re-schedules the command on ASB with an 8-second delay.
    /// </summary>
    public static async Task Handle(
        ILogger logger,
        IMessageBus messageBus,
        IDateTimeService dateTime,
        TopicSettings topicSettings,
        ICommonProducer commonKafkaProducer,
        IEmailServiceClient emailServiceClient,
        CheckEmailSendStatusCommand checkEmailSendStatusCommand)
    {
        if (checkEmailSendStatusCommand.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("NotificationId cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(checkEmailSendStatusCommand.SendOperationId))
        {
            throw new ArgumentException("SendOperationId cannot be null, empty, or whitespace");
        }

        EmailSendResult sendResult = await emailServiceClient.GetOperationUpdate(checkEmailSendStatusCommand.SendOperationId);

        if (sendResult != EmailSendResult.Sending)
        {
            var operationResult = new SendOperationResult()
            {
                SendResult = sendResult,
                OperationId = checkEmailSendStatusCommand.SendOperationId,
                NotificationId = checkEmailSendStatusCommand.NotificationId
            };

            // Terminal result published to Kafka — downstream status consumers have not yet migrated to ASB.
            await commonKafkaProducer.ProduceAsync(topicSettings.EmailStatusUpdatedTopicName, operationResult.Serialize());
        }
        else
        {
            var retryCommand = new CheckEmailSendStatusCommand
            {
                LastCheckedAtUtc = dateTime.UtcNow(),
                NotificationId = checkEmailSendStatusCommand.NotificationId,
                SendOperationId = checkEmailSendStatusCommand.SendOperationId
            };

            await messageBus.SendAsync(retryCommand, new DeliveryOptions { ScheduleDelay = TimeSpan.FromMilliseconds(_statusPollDelayMs) });
        }
    }
}
