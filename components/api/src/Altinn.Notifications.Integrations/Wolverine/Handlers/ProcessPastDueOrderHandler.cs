using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="ProcessPastDueOrderCommand"/> messages consumed from the ASB past-due orders queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ProcessPastDueOrderHandler
{
    /// <summary>
    /// Processes a past-due notification order by routing it through the appropriate channel service.
    /// On retry attempts (<see cref="Envelope.Attempts"/> &gt; 1), delegates to
    /// <see cref="IOrderProcessingService.ProcessOrderRetry"/> which handles send condition
    /// failures and platform dependency errors gracefully.
    /// </summary>
    public static async Task Handle(
        ProcessPastDueOrderCommand command,
        Envelope envelope,
        IOrderProcessingService orderProcessingService,
        ILogger logger)
    {
        if (envelope.Attempts > 1)
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

            throw new SendConditionInconclusiveException(
                $"Send condition check inconclusive for order {command.Order.Id}. Scheduling retry.");
        }
    }
}
