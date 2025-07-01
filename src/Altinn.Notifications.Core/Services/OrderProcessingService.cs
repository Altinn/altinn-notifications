using System.Diagnostics;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
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
    public async Task<NotificationOrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, false);

        switch (sendingConditionEvaluationResult)
        {
            case { IsRetryNeeded: false, IsSendConditionMet: false }:

                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);

                break;

            case { IsRetryNeeded: false, IsSendConditionMet: true }:

                switch (order.NotificationChannel)
                {
                    case NotificationChannel.Sms:
                        await _smsProcessingService.ProcessOrder(order);
                        break;

                    case NotificationChannel.Email:
                        await _emailProcessingService.ProcessOrder(order);
                        break;

                    case NotificationChannel.EmailAndSms:
                        await _emailAndSmsProcessingService.ProcessOrderAsync(order);
                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        await _preferredChannelProcessingService.ProcessOrder(order);
                        break;
                }

                await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);

                break;
        }

        return new NotificationOrderProcessingResult
        {
            IsRetryRequired = sendingConditionEvaluationResult.IsRetryNeeded
        };
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, true);

        switch (sendingConditionEvaluationResult)
        {
            case { IsRetryNeeded: false, IsSendConditionMet: false }:
                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
                break;

            case { IsRetryNeeded: false, IsSendConditionMet: true }:

                switch (order.NotificationChannel)
                {
                    case NotificationChannel.Sms:
                        await _smsProcessingService.ProcessOrderRetry(order);
                        break;

                    case NotificationChannel.Email:
                        await _emailProcessingService.ProcessOrderRetry(order);
                        break;

                    case NotificationChannel.EmailAndSms:
                        await _emailAndSmsProcessingService.ProcessOrderRetryAsync(order);
                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        await _preferredChannelProcessingService.ProcessOrderRetry(order);
                        break;
                }

                await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);
                break;
        }
    }

    /// <summary>
    /// Evaluates whether a notification order should be processed based on its configured condition endpoint.
    /// </summary>
    /// <param name="order">The notification order containing the condition endpoint to be checked.</param>
    /// <param name="isRetry">A boolean flag indicating whether this evaluation is part of a retry attempt.</param>
    /// <returns>
    /// A <see cref="SendConditionEvaluationResult"/> that contains:
    /// <list type="bullet">
    ///   <item><description>Whether the sending condition was met (<see cref="SendConditionEvaluationResult.IsSendConditionMet"/>)</description></item>
    ///   <item><description>Whether a retry should be attempted (<see cref="SendConditionEvaluationResult.IsRetryNeeded"/>)</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// If no condition endpoint is specified, the method returns a result indicating the condition is met and no retry is needed.
    /// </para>
    /// <para>
    /// During the first attempt (when <paramref name="isRetry"/> is <c>false</c>), if the condition check fails, the method recommends a retry.
    /// </para>
    /// <para>
    /// During a retry attempt (when <paramref name="isRetry"/> is <c>true</c>), if the condition check fails, the method allows the order to be processed anyway.
    /// </para>
    /// </remarks>
    private async Task<SendConditionEvaluationResult> EvaluateSendingCondition(NotificationOrder order, bool isRetry)
    {
        if (order.ConditionEndpoint == null)
        {
            return new SendConditionEvaluationResult { IsSendConditionMet = true };
        }

        var evaluatationResult = await _conditionClient.CheckSendCondition(order.ConditionEndpoint);

        return evaluatationResult.Match(
            checkResult =>
            {
                if (checkResult)
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

                return new SendConditionEvaluationResult { IsSendConditionMet = checkResult };
            },
            errorResult =>
            {
                if (isRetry)
                {
                    _logger.LogInformation(
                        "// OrderProcessingService // IsSendConditionMet // Condition check failed on retry for order with ID '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Processing the order regardless.",
                        order.Id,
                        order.ConditionEndpoint,
                        errorResult.StatusCode,
                        errorResult.Message ?? "No error message provided");

                    return new SendConditionEvaluationResult { IsSendConditionMet = true };
                }
                else
                {
                    _logger.LogInformation(
                        "// OrderProcessingService // IsSendConditionMet // Condition check failed for order '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Order will be sent to retry queue.",
                        order.Id,
                        order.ConditionEndpoint,
                        errorResult.StatusCode,
                        errorResult.Message ?? "No error message provided");

                    return new SendConditionEvaluationResult
                    {
                        IsRetryNeeded = true,
                        IsSendConditionMet = null // Inconclusive due to endpoint failure
                    };
                }
            });
    }
}
