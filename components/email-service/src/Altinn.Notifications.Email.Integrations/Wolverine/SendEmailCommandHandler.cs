using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Email.Integrations.Wolverine;

/// <summary>
/// Wolverine handler for <see cref="SendEmailCommand"/> messages received from Azure Service Bus.
/// Maps the shared command to the email-service domain model and delegates to <see cref="ISendingService"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SendEmailCommandHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static WolverineSettings Settings { get; set; }

    /// <summary>
    /// Configures error handling for the email delivery report queue handler.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        var policy = Settings.EmailSendQueuePolicy;

        chain
            .OnException<InvalidOperationException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .Or<ServiceBusException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Handles a <see cref="SendEmailCommand"/> by converting it to an <see cref="Email"/>
    /// and forwarding it to the sending service.
    /// </summary>
    /// <param name="command">The incoming send-email command.</param>
    /// <param name="sendingService">The email sending service.</param>
    /// <param name="logger">The logger.</param>
    public static async Task HandleAsync(
        SendEmailCommand command,
        ISendingService sendingService,
        ILogger<object> logger)
    {
        if (!Enum.TryParse<EmailContentType>(command.ContentType, ignoreCase: true, out var contentType))
        {
            logger.LogError(
                "SendEmailCommandHandler unknown ContentType '{ContentType}' for NotificationId {NotificationId}. Defaulting to Plain. ToAddress: {ToAddress}",
                command.ContentType,
                command.NotificationId,
                command.ToAddress);

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
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "SendEmailCommandHandler failed to send email for NotificationId: {NotificationId}. ToAddress: {ToAddress}, Error: {ErrorMessage}",
                command.NotificationId,
                command.ToAddress, 
                ex.Message);

            throw; // Re-throw to trigger Wolverine retry logic
        }
    }
}
