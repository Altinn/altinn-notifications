using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="ProcessPastDueOrderCommand"/> messages consumed from the ASB past-due orders queue.
/// </summary>
public static class ProcessPastDueOrderHandler
{
    /// <summary>
    /// Processes a past-due notification order by routing it through the appropriate channel service.
    /// On retry attempts (<see cref="ProcessPastDueOrderCommand.IsRetry"/> is <see langword="true"/>),
    /// delegates to <see cref="IOrderProcessingService.ProcessOrderRetry"/> which handles send condition
    /// failures and platform dependency errors gracefully.
    /// When the send condition check is inconclusive, schedules a new command with
    /// <see cref="ProcessPastDueOrderCommand.IsRetry"/> set to <see langword="true"/> after
    /// the configured <see cref="WolverineSettings.PastDueOrdersRetryDelayMs"/> delay.
    /// </summary>
    public static async Task Handle(
        ProcessPastDueOrderCommand command,
        IMessageContext messageContext,
        WolverineSettings settings,
        IOrderProcessingService orderProcessingService,
        ILogger logger)
    {
        if (command.IsRetry)
        {
            await orderProcessingService.ProcessOrderRetry(command.Order);
            return;
        }

        var result = await orderProcessingService.ProcessOrder(command.Order);

        if (result.IsRetryRequired)
        {
            logger.LogInformation(
                "Send condition check inconclusive for order {OrderId}, scheduling retry.",
                command.Order.Id);

            await messageContext.ScheduleAsync(
                command with { IsRetry = true },
                TimeSpan.FromMilliseconds(settings.PastDueOrdersRetryDelayMs));           
        }
    }
}
