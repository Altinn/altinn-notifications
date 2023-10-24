using System.Diagnostics;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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
    private readonly IEmailNotificationRepository _emailNotificationRepository;
    private readonly IEmailNotificationService _emailService;
    private readonly IKafkaProducer _producer;
    private readonly string _pastDueOrdersTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public OrderProcessingService(IOrderRepository orderRepository, IEmailNotificationRepository emailNotificationRepository, IEmailNotificationService emailService, IKafkaProducer producer, IOptions<KafkaSettings> kafkaSettings)
    {
        _orderRepository = orderRepository;
        _emailNotificationRepository = emailNotificationRepository;
        _emailService = emailService;
        _producer = producer;
        _pastDueOrdersTopic = kafkaSettings.Value.PastDueOrdersTopicName;
    }

    /// <inheritdoc/>
    public async Task StartProcessingPastDueOrders()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<NotificationOrder> pastDueOrders;
        do
        {
            pastDueOrders = await _orderRepository.GetPastDueOrdersAndSetProcessingState();

            foreach (NotificationOrder order in pastDueOrders)
            {
                bool success = await _producer.ProduceAsync(_pastDueOrdersTopic, order.Serialize());
                if (!success)
                {
                    await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered);
                }
            }
        }
        while (pastDueOrders.Count() >= 50 && sw.ElapsedMilliseconds < 60_000);

        sw.Stop();
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        NotificationChannel ch = order.NotificationChannel;

        foreach (Recipient recipient in order.Recipients)
        {
            switch (ch)
            {
                case NotificationChannel.Email:
                    await _emailService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
                    break;
            }
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        NotificationChannel ch = order.NotificationChannel;

        List<EmailRecipient> emailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);

        foreach (Recipient recipient in order.Recipients)
        {
            switch (ch)
            {
                case NotificationChannel.Email:
                    EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

                    if (!emailRecipients.Exists(er => er.RecipientId == (string.IsNullOrEmpty(recipient.RecipientId) ? null : recipient.RecipientId)
                        && er.ToAddress.Equals(addressPoint?.EmailAddress)))
                    {
                        await _emailService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
                    }

                    break;
            }
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }
}
