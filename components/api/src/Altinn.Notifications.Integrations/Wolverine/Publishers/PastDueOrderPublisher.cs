using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// ASB-backed implementation of <see cref="IPastDueOrderPublisher"/> that publishes
/// past-due orders concurrently to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
public class PastDueOrderPublisher(
    ILogger<PastDueOrderPublisher> logger,
    IMessageBusPublisher messageBusPublisher,
    IOptions<WolverineSettings> options) : IPastDueOrderPublisher
{
    private readonly ILogger<PastDueOrderPublisher> _logger = logger;
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;
    private readonly int _publishConcurrency = options.Value.PastDueOrdersPublishConcurrency <= 0 ? 10 : options.Value.PastDueOrdersPublishConcurrency;

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationOrder>> PublishAsync(
        IReadOnlyList<NotificationOrder> orders,
        CancellationToken cancellationToken = default)
    {
        return _messageBusPublisher.PublishBatchAsync(
            orders, 
            commandFactory: order => new ProcessPastDueOrderCommand { Order = order }, 
            onError: (order, ex) =>
            {
                if (ex is OperationCanceledException)
                {
                    _logger.LogInformation(
                        ex,
                        "PastDueOrderPublisher cancelled before publishing order {OrderId}; reporting as unpublished.",
                        order.Id);
                    return;
                }

                _logger.LogError(
                    ex,
                    "PastDueOrderPublisher failed to publish order {OrderId} to ASB queue",
                    order.Id);
            },
            cancellationToken);
    }
}
