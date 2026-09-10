using System.Diagnostics;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
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
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly IUnitOfWorkRepository _unitOfWorkRepository;
    private static readonly ActivitySource _activitySource = new("Altinn.Notifications.OrderProcessingService");

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
    public async Task<bool> StartProcessingPastDueOrders(bool processRetry, CancellationToken cancellationToken = default)
    {
        // TODO: pastdue poc: Change operation name to something more descriptive, e.g. "ProcessPastDueOrdersBatch"
        using Activity? activity = _activitySource.StartActivity("StartProcessingPastDueOrders.Loop.Iteration");
        UnitOfWork unitOfWork;
        try
        {
            unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Failed to start a unit of work for past due order processing.");
            }

            return false;
        }

        try
        {
            var pastDueOrder = await _orderRepository.GetNextPastDueOrder(unitOfWork, processRetry, cancellationToken);
            if (pastDueOrder == null)
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);

                return false;
            }

            await ProcessOrder(pastDueOrder, unitOfWork);
            await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

            return true;
        }
        catch (Exception e)
        {
            await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "An error occurred while processing past due order: {ErrorMessage}", e.Message);
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order, UnitOfWork unitOfWork)
    {
        var sendingConditionEvaluationResult = await EvaluateSendingCondition(order);

        switch (sendingConditionEvaluationResult)
        {
            case { IsSendConditionMet: false }:
            case { IsSendConditionMet: null }:
                var status = sendingConditionEvaluationResult.IsSendConditionMet == false ? OrderProcessingStatus.SendConditionNotMet : OrderProcessingStatus.Retrying;
                await _orderRepository.SetOrderSendConditionNotMetAsync(unitOfWork, order, status);
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
    }

    /// <summary>
    /// Determines if a notification order should proceed based on its configured send condition endpoint.
    /// </summary>
    /// <param name="order">The notification order containing the optional condition endpoint to evaluate.</param>
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
    private async Task<SendConditionEvaluationResult> EvaluateSendingCondition(NotificationOrder order)
    {
        if (order.ConditionEndpoint == null)
        {
            return new SendConditionEvaluationResult { IsSendConditionMet = true };
        }

        var evaluationResult = await _conditionClient.CheckSendCondition(order.ConditionEndpoint);

        if (evaluationResult.IsSuccess)
        {
            if (evaluationResult.Value)
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

            return new SendConditionEvaluationResult { IsSendConditionMet = evaluationResult.Value };
        }

        if (order.OrderProcessingStatus == OrderProcessingStatus.Retrying)
        {
            _logger.LogInformation(
                "// OrderProcessingService // IsSendConditionMet // Condition check failed on retry for order with ID '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Processing the order regardless.",
                order.Id,
                order.ConditionEndpoint,
                evaluationResult.Error!.StatusCode,
                evaluationResult.Error.Message ?? "No error message provided");

            return new SendConditionEvaluationResult { IsSendConditionMet = true };
        }
        else
        {
            _logger.LogInformation(
                "// OrderProcessingService // IsSendConditionMet // Condition check failed for order '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Order will be sent to retry queue.",
                order.Id,
                order.ConditionEndpoint,
                evaluationResult.Error!.StatusCode,
                evaluationResult.Error.Message ?? "No error message provided");

            return new SendConditionEvaluationResult
            {
                IsSendConditionMet = null // Inconclusive due to endpoint failure
            };
        }
    }
}
