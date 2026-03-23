using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Configuration;

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Sms.Integrations.Wolverine;

/// <summary>
/// Provides functionality to handle commands for sending SMS messages asynchronously.
/// </summary>
/// <remarks>This static class is intended to process SMS sending commands. All members are thread-safe and can be
/// used concurrently. Ensure that the provided command contains valid data before invoking handler methods.</remarks>
public static class SendSmsCommandHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings for configuring the behavior of the command handler, including error handling policies and retry strategies.
    /// </summary>
    public static WolverineSettings Settings { get; set; } = new();

    /// <summary>
    /// Configures error handling for the email delivery report queue handler.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        var policy = Settings.SmsSendQueuePolicy;

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
    /// Handles a <see cref="SendSmsCommand"/> by converting it to an <see cref="Sms"/>
    /// and forwarding it to the sending service.
    /// </summary>
    /// <param name="command">The incoming send-sms command.</param>
    /// <param name="sendingService">The sms sending service.</param>
    /// <param name="logger">The logger.</param>
    public static async Task HandleAsync(
        SendSmsCommand command,
        ISendingService sendingService,
        ILogger<object> logger)
    {
        var sms = new Core.Sending.Sms
        {
            Recipient = command.MobileNumber,
            Message = command.Body,
            Sender = command.SenderNumber,
            NotificationId = command.NotificationId
        };
        
        await sendingService.SendAsync(sms);
    }
}
