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
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly IUnitOfWorkRepository _unitOfWorkRepository;

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
        ILogger<OrderProcessingService> logger,
        IUnitOfWorkRepository unitOfWorkRepository)
    {
        _orderRepository = orderRepository;
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _preferredChannelProcessingService = preferredChannelProcessingService;
        _emailAndSmsProcessingService = emailAndSmsProcessingService;
        _conditionClient = conditionClient;
        _logger = logger;
        _unitOfWorkRepository = unitOfWorkRepository;
    }

    /// <inheritdoc/>
    public async Task StartProcessingPastDueOrders(CancellationToken cancellationToken = default)
    {
        // TODO: pastdue poc: Change operation name to something more descriptive, e.g. "ProcessPastDueOrdersBatch"
        using Activity? activityBatch = Activity.Current?.Source.StartActivity("StartProcessingPastDueOrders");
        Stopwatch stopwatch = Stopwatch.StartNew();
        int totalOrdersProcessed = 0;

        while (stopwatch.ElapsedMilliseconds < 60_000)
        {
            var unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();

            try
            {
                var pastDueOrder = await _orderRepository.GetNextPastDueOrder(unitOfWork, cancellationToken);
                if (pastDueOrder == null)
                {
                    await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                    break;
                }

                await ProcessOrder(pastDueOrder!, unitOfWork);
                await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

                cancellationToken.ThrowIfCancellationRequested();
                ++totalOrdersProcessed;
            }
            catch (Exception e)
            {
                Activity.Current?.SetTag("TotalCount", totalOrdersProcessed);

                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                Console.WriteLine(e.Message);

                throw;
            }
        }

        stopwatch.Stop();
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderProcessingResult> ProcessOrder(NotificationOrder order, UnitOfWork unitOfWork)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, false);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                // TODO pastdue poc: Decide how to reprocess orders that have failed the send condition check. For now, we will set the order to "SendConditionNotMet" and not retry it.
                await _orderRepository.SetOrderSendConditionNotMetAsync(unitOfWork, order);
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

                await _orderRepository.PersistProcessingResultAsync(unitOfWork, order, emailOrderProcessingResult, smsOrderProcessingResult);
                break;
        }

        return new NotificationOrderProcessingResult
        {
            IsRetryRequired = sendingConditionEvaluationResult.IsSendConditionMet is null
        };
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order, UnitOfWork unitOfWork)
    {
        try
        {
            await ProcessOrderRetryInternal(order, unitOfWork);
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
        }
    }

    private async Task ProcessOrderRetryInternal(NotificationOrder order, UnitOfWork unitOfWork)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order, true);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
                await _orderRepository.SetOrderSendConditionNotMetAsync(unitOfWork, order);
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

                await _orderRepository.PersistProcessingResultAsync(unitOfWork, order, emailOrderProcessingResult, smsOrderProcessingResult);
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
