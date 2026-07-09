using System.Diagnostics;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Logging;

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
    private readonly IPastDueOrderPublisher _pastDueOrderPublisher;
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
        IPastDueOrderPublisher pastDueOrderPublisher,
        ILogger<OrderProcessingService> logger)
    {
        _orderRepository = orderRepository;
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _preferredChannelProcessingService = preferredChannelProcessingService;
        _emailAndSmsProcessingService = emailAndSmsProcessingService;
        _conditionClient = conditionClient;
        _pastDueOrderPublisher = pastDueOrderPublisher;
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

            IReadOnlyList<NotificationOrder>? failedOrders = null;
            try
            {
                pastDueOrders = await _orderRepository.GetPastDueOrdersAndSetProcessingState(cancellationToken);
                if (pastDueOrders.Count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                failedOrders = await _pastDueOrderPublisher.PublishAsync(pastDueOrders, cancellationToken);
                await ResetOrdersToRegistered(failedOrders);
            }
            catch (OperationCanceledException)
            {
                // If PublishAsync completed, only reset orders it reported as failed — published orders
                // must not be reset or they will be re-enqueued and processed twice.
                // If PublishAsync never returned (threw mid-batch), reset all as we cannot tell which were published.
                var ordersToReset = failedOrders ?? pastDueOrders;
                await ResetOrdersToRegistered(ordersToReset);

                throw;
            }
        }
        while (pastDueOrders.Count >= 50 && stopwatch.ElapsedMilliseconds < 60_000);

        stopwatch.Stop();
    }

    /// <summary>
    /// Resets each of the given orders back to <see cref="OrderProcessingStatus.Registered"/>, one at a
    /// time. A failure resetting one order is logged and does not stop the remaining orders from being
    /// reset — without this, a single transient failure partway through the batch would silently leave
    /// every subsequent order stuck in <see cref="OrderProcessingStatus.Processing"/> indefinitely.
    /// </summary>
    private async Task ResetOrdersToRegistered(IEnumerable<NotificationOrder> orders)
    {
        foreach (var orderId in orders.Select(order => order.Id))
        {
            try
            {
                await _orderRepository.ResetProcessingToRegistered(orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to reset order {OrderId} back to Registered after a failed publish attempt. It may remain stuck in Processing until reconciled.",
                    orderId);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, false);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetOrderSendConditionNotMetAsync(order);
                break;

            case { IsSendConditionMet: true }:
                SmsOrderProcessingResult smsOrderProcessingResult = new([], null);
                EmailOrderProcessingResult emailOrderProcessingResult = new([], null);

                switch (order.NotificationChannel)
                {
                    case NotificationChannel.Sms:
                        var smsResult = await _smsProcessingService.ProcessOrder(order);
                        smsOrderProcessingResult = smsResult;
                        break;

                    case NotificationChannel.Email:
                        var emailResult = await _emailProcessingService.ProcessOrder(order);
                        emailOrderProcessingResult = emailResult;
                        break;

                    case NotificationChannel.EmailAndSms:
                        var emailAndSmsResult = await _emailAndSmsProcessingService.ProcessOrderAsync(order);
                        emailOrderProcessingResult = emailAndSmsResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = emailAndSmsResult.SmsOrderProcessingResult;
                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        var preferredResult = await _preferredChannelProcessingService.ProcessOrder(order);
                        emailOrderProcessingResult = preferredResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = preferredResult.SmsOrderProcessingResult;
                        break;
                }

                await _orderRepository.PersistProcessingResultAsync(order, emailOrderProcessingResult, smsOrderProcessingResult);
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
        try
        {
            await ProcessOrderRetryInternal(order);
        }
        catch (PlatformDependencyException e)
        {
            _logger.LogError(
               e,
               "Platform dependency '{DependencyName}' failed during '{Operation}' when retrying past due order {OrderId}. IsTransient: {IsTransient}",
               e.DependencyName,
               e.Operation,
               order!.Id,
               e.IsTransient?.ToString() ?? "Not available");

            await _orderRepository.ResetProcessingToRegistered(order.Id);
        }
    }

    private async Task ProcessOrderRetryInternal(NotificationOrder order)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, true);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetOrderSendConditionNotMetAsync(order);
                break;

            case { IsSendConditionMet: true }:
                EmailOrderProcessingResult emailOrderProcessingResult = new([], null);
                SmsOrderProcessingResult smsOrderProcessingResult = new([], null);

                switch (order.NotificationChannel)
                {
                    case NotificationChannel.Sms:
                        var smsResult = await _smsProcessingService.ProcessOrderRetry(order);
                        smsOrderProcessingResult = smsResult;
                        break;

                    case NotificationChannel.Email:
                        var emailResult = await _emailProcessingService.ProcessOrderRetry(order);
                        emailOrderProcessingResult = emailResult;
                        break;

                    case NotificationChannel.EmailAndSms:
                        var emailAndSmsResult = await _emailAndSmsProcessingService.ProcessOrderRetryAsync(order);
                        emailOrderProcessingResult = emailAndSmsResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = emailAndSmsResult.SmsOrderProcessingResult;
                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        var preferredResult = await _preferredChannelProcessingService.ProcessOrderRetry(order);
                        emailOrderProcessingResult = preferredResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = preferredResult.SmsOrderProcessingResult;
                        break;
                }

                await _orderRepository.PersistProcessingResultAsync(order, emailOrderProcessingResult, smsOrderProcessingResult);
                break;
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

        var evaluationResult = await _conditionClient.CheckSendCondition(order.ConditionEndpoint);

        return evaluationResult.Match(
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
