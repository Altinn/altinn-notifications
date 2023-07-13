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
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderService"/> class.
    /// </summary>
    public EmailNotificationOrderService(IOrderRepository repository, IGuidService guid, IDateTimeService dateTime)
    {
        _repository = repository;
        _guid = guid;
        _dateTime = dateTime;
    }

    /// <inheritdoc/>
    public async Task<(NotificationOrder? Order, ServiceError? Error)> RegisterEmailNotificationOrder(NotificationOrderRequest orderRequest)
    {
        string orderId = _guid.NewGuidAsString();
        DateTime created = _dateTime.UtcNow();

        var order = new NotificationOrder(
            orderId,
            orderRequest.SendersReference,
            orderRequest.Templates,
            orderRequest.SendTime,
            orderRequest.NotificationChannel,
            orderRequest.Creator,
            created,
            orderRequest.Recipients);

        NotificationOrder savedOrder = await _repository.Create(order);

        // push to kafka 
        return (savedOrder, null);
    }
}