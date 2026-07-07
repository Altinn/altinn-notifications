using System.Collections.Concurrent;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// ASB-backed implementation of <see cref="IPastDueOrderPublisher"/> that publishes
/// past-due orders concurrently to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class PastDueOrderPublisher(
    ILogger<PastDueOrderPublisher> logger,
    IServiceProvider serviceProvider,
    IOptions<WolverineSettings> options) : IPastDueOrderPublisher
{
    private readonly ILogger<PastDueOrderPublisher> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly int _publishConcurrency = options.Value.PastDueOrdersPublishConcurrency <= 0 ? 10 : options.Value.PastDueOrdersPublishConcurrency;

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

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failed = new ConcurrentBag<NotificationOrder>();
        using var semaphore = new SemaphoreSlim(_publishConcurrency);

        // None of the per-order work below is allowed to throw — including cancellation while
        // still queued on the semaphore — so Task.WhenAll always returns normally with a `failed`
        // list that is a complete and accurate record of every order that was not published.
        // Callers depend on that guarantee to safely retry only the orders that need it.
        await Task.WhenAll(orders.Select(async order =>
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                failed.Add(order);
                return;
            }

            try
            {
                var failedOrder = await SendAsync(order, messageBus, cancellationToken);
                if (failedOrder is not null)
                {
                    failed.Add(failedOrder);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return [.. failed];
    }

    private async Task<NotificationOrder?> SendAsync(
        NotificationOrder order,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await messageBus.SendAsync(new ProcessPastDueOrderCommand { Order = order });
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PastDueOrderPublisher failed to publish order {OrderId} to ASB queue",
                order.Id);
            return order;
        }
    }
}
