using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IEmailNotificationOrderService"/>. 
/// </summary>
public class EmailNotificationOrderService : IEmailNotificationOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;
    private readonly IApplicationOwnerConfigRepository _applicationOwnerConfigRepository;

    private readonly string _defaultFromAddress;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderService"/> class.
    /// </summary>
    public EmailNotificationOrderService(
        IOrderRepository repository,
        IGuidService guid,
        IDateTimeService dateTime,
        IApplicationOwnerConfigRepository applicationOwnerConfigRepository,
        IOptions<NotificationOrderConfig> config)
    {
        _repository = repository;
        _guid = guid;
        _dateTime = dateTime;
        _applicationOwnerConfigRepository = applicationOwnerConfigRepository;
        _defaultFromAddress = config.Value.DefaultEmailFromAddress;
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrder, ServiceError>> RegisterEmailNotificationOrder(NotificationOrderRequest orderRequest)
    {
        ApplicationOwnerConfig? config = 
            await _applicationOwnerConfigRepository.GetApplicationOwnerConfig(orderRequest.Creator.ShortName);

        foreach (var template in orderRequest.Templates.OfType<EmailTemplate>())
        {
            if (string.IsNullOrEmpty(template.FromAddress))
            {
                template.FromAddress = _defaultFromAddress;
            }
            else
            {
                if (config is not null && !config.EmailAddresses.Contains(template.FromAddress))
                {
                    return new ServiceError(400, "All given from address must be registered as approved by Altinn.");
                }
            }
        }

        Guid orderId = _guid.NewGuid();
        DateTime created = _dateTime.UtcNow();

        var order = new NotificationOrder(
            orderId,
            orderRequest.SendersReference,
            orderRequest.Templates,
            orderRequest.RequestedSendTime,
            orderRequest.NotificationChannel,
            orderRequest.Creator,
            created,
            orderRequest.Recipients);

        NotificationOrder savedOrder = await _repository.Create(order);

        return savedOrder;
    }
}
