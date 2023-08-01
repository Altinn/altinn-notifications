using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderService"/>
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderService"/> class.
    /// </summary>
    public OrderService(IOrderRepository repo)
    {
        _repo = repo;
    }

    /// <inheritdoc/>
    public async Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderById(Guid id, string creator)
    {
        NotificationOrder? order = await _repo.GetOrderById(id, creator);

        if (order == null)
        {
            return (null, new ServiceError(404));
        }

        return (order, null);
    }

    /// <inheritdoc/>
    public async Task<(List<NotificationOrder>? Orders, ServiceError? Error)> GetOrdersBySendersReference(string senderRef, string creator)
    {
        List<NotificationOrder> orders = await _repo.GetOrdersBySendersReference(senderRef, creator);

        return (orders, null);
    }
}