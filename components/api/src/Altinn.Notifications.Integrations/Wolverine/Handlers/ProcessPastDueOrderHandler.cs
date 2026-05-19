using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
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
    /// On retry attempts (<see cref="ProcessPastDueOrderCommand.IsProcessOrderRetry"/> is <see langword="true"/>),
    /// delegates to <see cref="IOrderProcessingService.ProcessOrderRetry"/> which handles send condition
    /// failures and platform dependency errors gracefully.
    /// When the send condition check is inconclusive, schedules a new command with
    /// <see cref="ProcessPastDueOrderCommand.IsProcessOrderRetry"/> set to <see langword="true"/> after
    /// the configured <see cref="WolverineSettings.PastDueOrdersRetryDelayMs"/> delay.
    /// </summary>
    public static async Task Handle(
        ProcessPastDueOrderCommand command,
        IMessageContext messageContext,
        WolverineSettings settings,
        IOrderProcessingService orderProcessingService,
        ILogger logger)
    {
        if (command.IsProcessOrderRetry)
        {
            await orderProcessingService.ProcessOrderRetry(command.Order);
            return;
        }

        NotificationOrderProcessingResult result;

        try
        {
            result = await orderProcessingService.ProcessOrder(command.Order);
        }
        catch (PlatformDependencyException ex)
        {
            logger.LogWarning(
                ex,
                "Platform dependency '{DependencyName}' failed during '{Operation}' for order {OrderId}. Scheduling retry.",
                ex.DependencyName,
                ex.Operation,
                command.Order.Id);

            await messageContext.ScheduleAsync(
                command with { IsProcessOrderRetry = true },
                TimeSpan.FromMilliseconds(settings.PastDueOrdersRetryDelayMs));

            return;
        }

        if (result.IsRetryRequired)
        {
            await messageContext.ScheduleAsync(
                command with { IsProcessOrderRetry = true },
                TimeSpan.FromMilliseconds(settings.PastDueOrdersRetryDelayMs));
        }
    }
}
