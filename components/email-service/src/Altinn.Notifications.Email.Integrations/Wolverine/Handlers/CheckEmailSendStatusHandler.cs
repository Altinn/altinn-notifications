using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Configuration;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wolverine;
using Wolverine.Attributes;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="CheckEmailSendStatusCommand"/> by polling ACS for email delivery status.
/// </summary>
[WolverineHandler]
public static class CheckEmailSendStatusHandler
{
    private const int _statusPollDelayMs = 8000;

    /// <summary>
    /// Configures error-handling and retry policies for the polling-loop handler chain.
    /// Wolverine resolves <paramref name="options"/> from the IoC container at chain-compilation time.
    /// </summary>
    /// <remarks>
    /// <see cref="ArgumentException"/> is intentionally excluded from the retry policy.
    /// Messages that fail guard-clause validation are malformed and must not be retried — they
    /// will be moved to the dead-letter queue by Wolverine's default failure handling.
    /// </remarks>
    /// <param name="chain">The Wolverine handler chain to apply the retry policies to.</param>
    /// <param name="options">The Wolverine settings resolved from the IoC container.</param>
    [ExcludeFromCodeCoverage]
    public static void Configure(HandlerChain chain, IOptions<WolverineSettings> options)
    {
        var policy = options.Value.CheckEmailSendStatusQueuePolicy;

        chain
            .OnException<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<TaskCanceledException>()
            .Or<InvalidOperationException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Polls ACS for delivery status. If the result is terminal, publishes
    /// a <see cref="SendOperationResult"/> to Kafka so downstream consumers can update
    /// the notification status. If still sending, re-schedules the command on ASB
    /// with an 8-second delay so the polling loop continues.
    /// </summary>
    public static async Task Handle(
        CheckEmailSendStatusCommand checkEmailSendStatusCommand,
        ILogger logger,
        IDateTimeService dateTime,
        TopicSettings topicSettings,
        IMessageContext messageContext,
        ICommonProducer commonKafkaProducer,
        IEmailServiceClient emailServiceClient)
    {
        if (checkEmailSendStatusCommand.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("NotificationId cannot be empty.", nameof(checkEmailSendStatusCommand));
        }

        if (string.IsNullOrWhiteSpace(checkEmailSendStatusCommand.SendOperationId))
        {
            throw new ArgumentException("SendOperationId cannot be null, empty, or whitespace.", nameof(checkEmailSendStatusCommand));
        }

        EmailSendResult sendResult = await emailServiceClient.GetOperationUpdate(checkEmailSendStatusCommand.SendOperationId);

        if (sendResult != EmailSendResult.Sending)
        {
            logger.LogWarning(
                "CheckEmailSendStatusHandler // Handle // Terminal status {SendResult} for NotificationId {NotificationId}.",
                sendResult,
                checkEmailSendStatusCommand.NotificationId);

            var operationResult = new SendOperationResult
            {
                SendResult = sendResult,
                OperationId = checkEmailSendStatusCommand.SendOperationId,
                NotificationId = checkEmailSendStatusCommand.NotificationId
            };

            await commonKafkaProducer.ProduceAsync(topicSettings.EmailStatusUpdatedTopicName, operationResult.Serialize());
        }
        else
        {
            logger.LogWarning(
                "CheckEmailSendStatusHandler // Handle // Still sending for NotificationId {NotificationId}. Scheduling re-check in {DelayMs} ms.",
                checkEmailSendStatusCommand.NotificationId,
                _statusPollDelayMs);

            var recheckEmailSendStatusCommand = new CheckEmailSendStatusCommand
            {
                LastCheckedAtUtc = dateTime.UtcNow(),
                NotificationId = checkEmailSendStatusCommand.NotificationId,
                SendOperationId = checkEmailSendStatusCommand.SendOperationId
            };

            await messageContext.ScheduleAsync(recheckEmailSendStatusCommand, TimeSpan.FromMilliseconds(_statusPollDelayMs));
        }
    }
}
