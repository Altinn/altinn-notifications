using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Email.Integrations.Wolverine;

/// <summary>
/// Wolverine handler for <see cref="SendEmailCommand"/> messages received from Azure Service Bus.
/// </summary>
public static class SendEmailCommandHandler
{
    /// <summary>
    /// Configures error-handling and retry policies for the email send queue handler.
    /// Wolverine resolves <paramref name="options"/> from the IoC container at chain-compilation time.
    /// </summary>
    /// <param name="chain">The Wolverine handler chain to apply the retry policies to.</param>
    /// <param name="options">The Wolverine settings resolved from the IoC container.</param>
    [ExcludeFromCodeCoverage]
    public static void Configure(HandlerChain chain, IOptions<WolverineSettings> options)
    {
        var policy = options.Value.EmailSendQueuePolicy;

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
    /// Handles a <see cref="SendEmailCommand"/> by mapping it to an <see cref="Core.Sending.Email"/>
    /// and delegating sending to <see cref="ISendingService"/>.
    /// </summary>
    /// <param name="command">The send-email command to process.</param>
    /// <param name="sendingService">The service responsible for sending the email.</param>
    /// <param name="logger">The logger used to record processing errors.</param>
    public static async Task HandleAsync(SendEmailCommand command, ISendingService sendingService, ILogger logger)
    {
        if (!Enum.TryParse<EmailContentType>(command.ContentType, ignoreCase: true, out var contentType))
        {
            logger.LogError(
                "SendEmailCommandHandler unknown ContentType for NotificationId {NotificationId}. Defaulting to Plain.",
                command.NotificationId);

            contentType = EmailContentType.Plain;
        }

        var email = new Core.Sending.Email(
            command.NotificationId,
            command.Subject,
            command.Body,
            command.FromAddress,
            command.ToAddress,
            contentType);

        try
        {
            await sendingService.SendAsync(email);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOnSendEmailFailed(logger, ex, command.NotificationId);

            throw;
        }
    }

    /// <summary>
    /// Logs a send-email failure at error level.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="notificationId">The notification ID associated with the failed send attempt.</param>
    private static void LogOnSendEmailFailed(ILogger logger, Exception exception, Guid notificationId)
    {
        logger.LogError(
            exception,
            "SendEmailCommandHandler failed to send email for NotificationId: {NotificationId}.",
            notificationId);
    }
}
