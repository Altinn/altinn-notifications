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
            case { IsSendConditionMet: false }:
                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
                await TryInsertStatusFeedForUnmetCondition(order.Id);
                break;

            case { IsSendConditionMet: true }:

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

                var isOrderCompleted = await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);
                if (isOrderCompleted)
                {
                    await TryInsertStatusFeedForUnmetCondition(order.Id);
                }

                break;
        }

        return new NotificationOrderProcessingResult
        {
            IsRetryRequired = sendingConditionEvaluationResult.IsSendConditionMet is null
        };
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, true);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
                await TryInsertStatusFeedForUnmetCondition(order.Id);
                break;

            case { IsSendConditionMet: true }:

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

                var isOrderCompleted = await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);
                if (isOrderCompleted)
                {
                    await TryInsertStatusFeedForUnmetCondition(order.Id);
                }

                break;
        }
    }

    /// <summary>
    /// Attempts to insert a status feed entry for an order where the send condition was not met.
    /// Logs a warning if the insertion fails but does not throw, allowing order processing to continue.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order.</param>
    private async Task TryInsertStatusFeedForUnmetCondition(Guid orderId)
    {
        try
        {
            await _orderRepository.InsertStatusFeedForOrder(orderId);
        }
        catch (Exception ex)
        {
            var maskedOrderId = string.Concat(orderId.ToString().AsSpan(0, 8), "****");
            _logger.LogWarning(ex, "Failed to insert status feed for order {OrderId} after marking SendConditionNotMet.", maskedOrderId);
        }
    }

    /// <summary>
    /// Determines if a notification order should proceed based on its configured send condition endpoint.
    /// </summary>
    /// <param name="order">The notification order containing the optional condition endpoint to evaluate.</param>
    /// <param name="isRetry">
    /// Indicates whether this evaluation is part of a retry attempt.
    /// If <c>false</c>, a failed or inconclusive condition check will result in a retry recommendation.
    /// If <c>true</c>, the order will be processed even if the condition check fails.
    /// </param>
    /// <returns>
    /// A <see cref="SendConditionEvaluationResult"/> indicating:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="SendConditionEvaluationResult.IsSendConditionMet"/>:
    ///       <c>true</c> if the send condition is met or no endpoint is specified;
    ///       <c>false</c> if the condition is not met;
    ///       <c>null</c> if the condition could not be evaluated due to an error (only on first attempt).
    ///     </description>
    ///   </item>
    /// </list>
    /// </returns>
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
                        IsSendConditionMet = null // Inconclusive due to endpoint failure
                    };
                }
            });
    }
}
