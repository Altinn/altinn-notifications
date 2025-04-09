using System.Diagnostics;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
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
    private readonly IPreferredChannelProcessingService _preferredChannelProcessingService;
    private readonly IBothChannelsProcessingService _bothChannelsProcessingService;
    private readonly IConditionClient _conditionClient;
    private readonly IKafkaProducer _producer;
    private readonly string _pastDueOrdersTopic;
    private readonly ILogger<OrderProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public OrderProcessingService(
        IOrderRepository orderRepository,
        IEmailOrderProcessingService emailProcessingService,
        ISmsOrderProcessingService smsProcessingService,
        IPreferredChannelProcessingService preferredChannelProcessingService,
        IBothChannelsProcessingService bothChannelsProcessingService,
        IConditionClient conditionClient,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<OrderProcessingService> logger)
    {
        _orderRepository = orderRepository;
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _preferredChannelProcessingService = preferredChannelProcessingService;
        _bothChannelsProcessingService = bothChannelsProcessingService;
        _conditionClient = conditionClient;
        _producer = producer;
        _pastDueOrdersTopic = kafkaSettings.Value.PastDueOrdersTopicName;
        _logger = logger;
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
        if (!await IsSendConditionMet(order, isRetry: false))
        {
            await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
            return;
        }

        NotificationChannel ch = order.NotificationChannel;

        switch (ch)
        {
            case NotificationChannel.Email:
                await _emailProcessingService.ProcessOrder(order);
                break;

            case NotificationChannel.Sms:
                await _smsProcessingService.ProcessOrder(order);
                break;

            case NotificationChannel.EmailAndSms:
                await _bothChannelsProcessingService.ProcessOrder(order);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
                await _preferredChannelProcessingService.ProcessOrder(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        if (!await IsSendConditionMet(order, isRetry: true))
        {
            await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
            return;
        }

        NotificationChannel ch = order.NotificationChannel;

        switch (ch)
        {
            case NotificationChannel.Email:
                await _emailProcessingService.ProcessOrderRetry(order);
                break;

            case NotificationChannel.Sms:
                await _smsProcessingService.ProcessOrderRetry(order);
                break;

            case NotificationChannel.EmailAndSms:
                await _bothChannelsProcessingService.ProcessOrder(order);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
                await _preferredChannelProcessingService.ProcessOrderRetry(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
    }

    /// <summary>
    /// Checks the send condition provided by the order request to determine if condition is met
    /// </summary>
    /// <param name="order">The notification order to check</param>
    /// <param name="isRetry">Boolean indicating if this is a retry attempt</param>
    /// <returns>True if condition is met and processing should continue</returns>
    /// <exception cref="OrderProcessingException">Throws an exception if failure on first attempt ot check condition</exception>
    internal async Task<bool> IsSendConditionMet(NotificationOrder order, bool isRetry)
    {
        if (order.ConditionEndpoint == null)
        {
            return true;
        }

        var conditionCheckResult = await _conditionClient.CheckSendCondition(order.ConditionEndpoint);

        return conditionCheckResult.Match(
          successResult =>
          {
              return successResult;
          },
          errorResult =>
          {
              if (!isRetry)
              {
                  // Always send to retry on first error. Exception is caught by consumer and message is moved to retry topic.
                  throw new OrderProcessingException($"// OrderProcessingService // IsSendConditionMet // Condition check for order with ID '{order.Id}' failed with HTTP status code '{errorResult.StatusCode}' at endpoint '{order.ConditionEndpoint}'");
              }

              // notifications should always be created and sent if the condition check is not successful
              _logger.LogInformation("// OrderProcessingService // IsSendConditionMet // Condition check for order with ID '{ID}' failed on retry. Processing regardless.", order.Id);
              return true;
          });
    }
}
