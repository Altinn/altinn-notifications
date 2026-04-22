using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// ASB-backed implementation of <see cref="IPastDueOrderPublisher"/> that publishes
/// past-due orders one-by-one to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class PastDueOrderPublisher(
    ILogger<PastDueOrderPublisher> logger,
    IServiceProvider serviceProvider) : IPastDueOrderPublisher
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationOrder>> PublishAsync(
        IReadOnlyList<NotificationOrder> orders,
        CancellationToken cancellationToken = default)
    {
        if (orders.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failed = new List<NotificationOrder>();
        foreach (var order in orders)
        {
            try
            {
                await messageBus.SendAsync(new ProcessPastDueOrderCommand { Order = order });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "PastDueOrderPublisher failed to publish order {OrderId} to ASB queue.",
                    order.Id);
                failed.Add(order);
            }
        }

        return failed;
    }
}
