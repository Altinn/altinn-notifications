using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IGetOrderService"/>
/// </summary>
public class GetOrderService : IGetOrderService
{
    private readonly IOrderRepository _repo;
    private readonly static Dictionary<OrderProcessingStatus, string> _descriptions = new()
    {
        { OrderProcessingStatus.Cancelled, "Order processing was stopped due to order being cancelled." },
        { OrderProcessingStatus.Processing, "Order processing is ongoing. Notifications are being generated." },
        { OrderProcessingStatus.Processed, "Order processing is done. Notifications have been successfully generated." },
        { OrderProcessingStatus.Completed, "Order processing is completed. All notifications have a final status." },
        { OrderProcessingStatus.SendConditionNotMet, "Order processing was stopped due to send condition not being met." },
        { OrderProcessingStatus.Registered, "Order has been registered and is awaiting requested send time before processing." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrderService"/> class.
    /// </summary>
    public GetOrderService(IOrderRepository repo)
    {
        _repo = repo;
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrder, ServiceError>> GetOrderById(Guid id, string creator)
    {
        NotificationOrder? order = await _repo.GetOrderById(id, creator);

        if (order == null)
        {
            return new ServiceError(404);
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task<Result<List<NotificationOrder>, ServiceError>> GetOrdersBySendersReference(string senderRef, string creator)
    {
        List<NotificationOrder> orders = await _repo.GetOrdersBySendersReference(senderRef, creator);

        return orders;
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrderWithStatus, ServiceError>> GetOrderWithStatuById(Guid id, string creator)
    {
        NotificationOrderWithStatus? order = await _repo.GetOrderWithStatusById(id, creator);

        if (order == null)
        {
            return new ServiceError(404);
        }

        order.ProcessingStatus.StatusDescription = GetStatusDescription(order.ProcessingStatus.Status);
        return order;
    }

    /// <summary>
    /// Gets the English description of the <see cref="OrderProcessingStatus"/>"
    /// </summary>
    internal static string GetStatusDescription(OrderProcessingStatus result)
    {
        return _descriptions[result];
    }
}
