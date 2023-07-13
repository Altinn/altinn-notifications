using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IEmailNotificationOrderService"/>. 
/// </summary>
public class EmailNotificationOrderService : IEmailNotificationOrderService
{
    private readonly IOrderRepository _repository;

    public EmailNotificationOrderService(IOrderRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public Task<(NotificationOrder? Order, ServiceError? Error)> RegisterEmailNotificationOrder(NotificationOrderRequest orderRequest)
    {
        throw new NotImplementedException();
    }
}