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
    public Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderById(Guid id)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderBySendersReference(string senderRef)
    {
        throw new NotImplementedException();
    }
}
