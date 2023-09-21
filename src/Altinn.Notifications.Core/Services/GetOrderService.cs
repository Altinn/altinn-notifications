using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IGetOrderService"/>
/// </summary>
public class GetOrderService : IGetOrderService
{
    private readonly IOrderRepository _repo;
    private readonly Dictionary<OrderProcessingStatus, string> _descriptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrderService"/> class.
    /// </summary>
    public GetOrderService(IOrderRepository repo)
    {
        _repo = repo;
        _descriptions = new()
        {
            { OrderProcessingStatus.Registered, "Order has been registered and is awaiting requested send time before processing" },
            { OrderProcessingStatus.Processing, "Order processing is ongoing. Notifications are being generated." },
            { OrderProcessingStatus.Completed, "Order processing is completed. All notifications have been generated." },
        };
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

    /// <inheritdoc/>
    public async Task<(NotificationOrderWithStatus? Order, ServiceError? Error)> GetOrderWithStatuById(Guid id, string creator)
    {
        NotificationOrderWithStatus? order = await _repo.GetOrderWithStatusById(id, creator);

        if (order == null)
        {
            return (null, new ServiceError(404));
        }

        order.ProcessingStatus.StatusDescription = _descriptions[order.ProcessingStatus.Status];
        return (order, null);
    }
}
