using System.Diagnostics;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderProcessingService"/>
/// </summary>
public class OrderProcessingService : IOrderProcessingService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEmailOrderProcessingService _emailProcessingService;
    private readonly ISmsOrderProcessingService _smsProcessingService;
    private readonly IKafkaProducer _producer;
    private readonly string _pastDueOrdersTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public OrderProcessingService(
        IOrderRepository orderRepository,
        IEmailOrderProcessingService emailProcessingService,
        ISmsOrderProcessingService smsProcessingService,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _orderRepository = orderRepository;
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
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
        while (pastDueOrders.Count >= 50 && sw.ElapsedMilliseconds < 60_000);

        sw.Stop();
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        NotificationChannel ch = order.NotificationChannel;

        switch (ch)
        {
            case NotificationChannel.Email:
                await _emailProcessingService.ProcessOrder(order);
                break;
            case NotificationChannel.Sms:
                await _smsProcessingService.ProcessOrder(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        NotificationChannel ch = order.NotificationChannel;

        switch (ch)
        {
            case NotificationChannel.Email:
                await _emailProcessingService.ProcessOrderRetry(order);
                break;
            case NotificationChannel.Sms:
                await _smsProcessingService.ProcessOrderRetry(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }
}
