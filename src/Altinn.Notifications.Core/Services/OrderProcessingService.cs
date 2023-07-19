using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderProcessingService"/>
/// </summary>
public class OrderProcessingService : IOrderProcessingService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEmailNotificationService _emailService;
    private readonly IKafkaProducer _producer;
    private readonly string _pastDueOrdersTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public OrderProcessingService(IOrderRepository orderRepository, IEmailNotificationService emailService, IKafkaProducer producer, IOptions<KafkaSettings> kafkaSettings)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
        _producer = producer;
        _pastDueOrdersTopic = kafkaSettings.Value.PastDueOrdersTopicName;
    }

    /// <inheritdoc/>
    public async Task StartProcessingPastDueOrders()
    {
        List<NotificationOrder> pastDueOrders = await _orderRepository.GetPastDueOrdersAndSetProcessingState();

        foreach (NotificationOrder order in pastDueOrders)
        {
            await _producer.ProduceAsync(_pastDueOrdersTopic, order.Serialize());
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
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
                        await _emailService.CreateEmailNotification(order.Id, order.RequestedSendTime, emailTemplate, recipient);
                    }

                    break;
            }
        }

        await _orderRepository.SetProcessingCompleted(order.Id);
    }
}