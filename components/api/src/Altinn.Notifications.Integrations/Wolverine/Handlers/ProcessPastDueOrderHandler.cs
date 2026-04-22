using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="ProcessPastDueOrderCommand"/> messages consumed from the ASB past-due orders queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ProcessPastDueOrderHandler
{
    /// <summary>
    /// Processes a past-due notification order by routing it through the appropriate channel service.
    /// </summary>
    public static async Task Handle(
        ProcessPastDueOrderCommand command,
        IOrderProcessingService orderProcessingService,
        ILogger logger)
    {
        var result = await orderProcessingService.ProcessOrder(command.Order);

        if (result.IsRetryRequired)
        {
            logger.LogInformation(
                "Send condition check inconclusive for order {OrderId}, scheduling retry.",
                command.Order.Id);

            throw new InvalidOperationException(
                $"Send condition check inconclusive for order {command.Order.Id}. Scheduling retry.");
        }
    }
}
