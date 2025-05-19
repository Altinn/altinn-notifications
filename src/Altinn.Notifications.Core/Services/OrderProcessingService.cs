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
    private readonly IEmailAndSmsOrderProcessingService _emailAndSmsProcessingService;
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
        IEmailAndSmsOrderProcessingService emailAndSmsProcessingService,
        IConditionClient conditionClient,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<OrderProcessingService> logger)
    {
        _orderRepository = orderRepository;
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _preferredChannelProcessingService = preferredChannelProcessingService;
        _emailAndSmsProcessingService = emailAndSmsProcessingService;
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
        if (!await IsSendConditionMet(order, false))
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
                await _emailAndSmsProcessingService.ProcessOrderAsync(order);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
                await _preferredChannelProcessingService.ProcessOrder(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);
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
                await _emailAndSmsProcessingService.ProcessOrderRetryAsync(order);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
                await _preferredChannelProcessingService.ProcessOrderRetry(order);
                break;
        }

        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);
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
                if (successResult)
                {
                    _logger.LogDebug(
                        "// OrderProcessingService // IsSendConditionMet // Condition check yield true for order '{OrderId}' at endpoint '{Endpoint}'.",
                        order.Id,
                        order.ConditionEndpoint);
                }
                else
                {
                    _logger.LogInformation(
                        "// OrderProcessingService // IsSendConditionMet // Condition check yield false for order '{OrderId}' at endpoint '{Endpoint}'.",
                        order.Id,
                        order.ConditionEndpoint);
                }

                return successResult;
            },
            errorResult =>
            {
                if (!isRetry)
                {
                    _logger.LogInformation(
                        "// OrderProcessingService // IsSendConditionMet // Condition check failed for order '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Order will be sent to retry queue.",
                        order.Id,
                        order.ConditionEndpoint,
                        errorResult.StatusCode,
                        errorResult.Message ?? "No error message provided");

                    // Exception is caught by consumer and message is moved to retry topic
                    throw new OrderProcessingException(
                        $"// OrderProcessingService // IsSendConditionMet // Condition check for order with ID '{order.Id}' failed with HTTP status code '{errorResult.StatusCode}' at endpoint '{order.ConditionEndpoint}'");
                }

                _logger.LogInformation(
                    "// OrderProcessingService // IsSendConditionMet // Condition check failed on retry for order with ID '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Processing the order regardless.",
                    order.Id,
                    order.ConditionEndpoint,
                    errorResult.StatusCode,
                    errorResult.Message ?? "No error message provided");

                // On retry, notifications should be sent even if condition check fails
                return true;
            });
    }
}
