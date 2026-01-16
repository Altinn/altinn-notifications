using System.Diagnostics;
using System.Text.Json;

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
    public async Task StartProcessingPastDueOrders(CancellationToken cancellationToken = default)
    {
        List<NotificationOrder> pastDueOrders;
        Stopwatch stopwatch = Stopwatch.StartNew();

        do
        {
            pastDueOrders = [];

            try
            {
                pastDueOrders = await _orderRepository.GetPastDueOrdersAndSetProcessingState(cancellationToken);
                if (pastDueOrders.Count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var serializedPastDueOrders = pastDueOrders.Select(e => e.Serialize());
                var unpublishedPastDueOrders = await _producer.ProduceAsync(_pastDueOrdersTopic, [.. serializedPastDueOrders], cancellationToken);

                foreach (var unpublishedPastDueOrder in unpublishedPastDueOrders)
                {
                    var deserializePastDueOrder = JsonSerializer.Deserialize<NotificationOrder>(unpublishedPastDueOrder, JsonSerializerOptionsProvider.Options);
                    if (deserializePastDueOrder == null || deserializePastDueOrder.Id == Guid.Empty)
                    {
                        continue;
                    }

                    await _orderRepository.SetProcessingStatus(deserializePastDueOrder.Id, OrderProcessingStatus.Registered);
                }
            }
            catch (OperationCanceledException)
            {
                foreach (var pastDueOrder in pastDueOrders)
                {
                    await _orderRepository.SetProcessingStatus(pastDueOrder.Id, OrderProcessingStatus.Registered);
                }

                throw;
            }
        }
        while (pastDueOrders.Count >= 50 && stopwatch.ElapsedMilliseconds < 60_000);

        stopwatch.Stop();
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, false);
        var isOrderCompleted = false;

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
                isOrderCompleted = true;
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

                isOrderCompleted = await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);
                break;
        }

        if (isOrderCompleted)
        {
            await TryInsertStatusFeedForCompletedOrder(order.Id);
        }

        return new NotificationOrderProcessingResult
        {
            IsRetryRequired = sendingConditionEvaluationResult.IsSendConditionMet is null
        };
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        var isOrderCompleted = false;
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, true);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
                isOrderCompleted = true;
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

                isOrderCompleted = await _orderRepository.TryCompleteOrderBasedOnNotificationsState(order.Id, AlternateIdentifierSource.Order);
                break;
        }

        if (isOrderCompleted)
        {
            await TryInsertStatusFeedForCompletedOrder(order.Id);
        }
    }

    /// <summary>
    /// Attempts to insert a status feed entry for a completed order.
    /// Logs a warning if the insertion fails but does not throw, allowing order processing to continue.
    /// </summary>
    /// <param name="orderId">The unique identifier of the completed order.</param>
    private async Task TryInsertStatusFeedForCompletedOrder(Guid orderId)
    {
        try
        {
            await _orderRepository.InsertStatusFeedForOrder(orderId);
        }
        catch (Exception ex)
        {
            var maskedOrderId = string.Concat(orderId.ToString().AsSpan(0, 8), "****");
            _logger.LogWarning(ex, "Failed to insert status feed for completed order {OrderId}.", maskedOrderId);
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
                    _logger.LogTrace(
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
