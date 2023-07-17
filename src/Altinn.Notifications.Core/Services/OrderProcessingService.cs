using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderProcessingService"/>
/// </summary>
public class OrderProcessingService : IOrderProcessingService
{
    private readonly IEmailNotificationService _emailService;
    private readonly IOrderRepository _orderRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public OrderProcessingService(IOrderRepository orderRepository, IEmailNotificationService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task ProcessPastDueOrders()
    {
        List<NotificationOrder> pastDueOrders = await _orderRepository.GetPastDueOrdersAndSetProcessingState();

        foreach (NotificationOrder order in pastDueOrders)
        {
            NotificationChannel ch = order.NotificationChannel;
            EmailTemplate? emailTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Email) as EmailTemplate;

            foreach (Recipient recipient in order.Recipients)
            {
                switch (ch)
                {
                    case NotificationChannel.Email:
                        if (emailTemplate != null)
                        {
                            _emailService.ProcessEmailNotification(order.Id, emailTemplate, recipient);
                        }

                        break;
                }
            }

            await _orderRepository.SetProcessingCompleted(order.Id);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessPendingOrders()
    {
        List<NotificationOrder> pastDueOrders = await _orderRepository.GetPendingOrdersAndSetProcessingState();

        foreach (NotificationOrder order in pastDueOrders)
        {
            NotificationChannel ch = order.NotificationChannel;

            switch (ch)
            {
                case NotificationChannel.Email:
                    List<EmailNotification> generatedEmailNotifiations = null; // Repository.GetAllEmailNotificationsForOrder();

                    // hvis det er en recipient det ikke er generert notificaion for, opprett notification og følg vanlig flyt
                    break;
            }

            await _orderRepository.SetProcessingCompleted(order.Id);
        }
    }
}