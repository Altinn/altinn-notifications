using System.Diagnostics;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderProcessingService"/>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
/// </remarks>
public class OrderProcessingService(
    IOrderRepository orderRepository,
    IEmailOrderProcessingService emailProcessingService,
    ISmsOrderProcessingService smsProcessingService,
    IPreferredChannelProcessingService preferredChannelProcessingService,
    IEmailAndSmsOrderProcessingService emailAndSmsProcessingService,
    IConditionClient conditionClient,
    ILogger<OrderProcessingService> logger,
    IUnitOfWorkRepository unitOfWorkRepository) : IOrderProcessingService
{
    private static readonly ActivitySource _activitySource = new("Altinn.Notifications.OrderProcessingService");

    /// <inheritdoc/>
    public async Task<bool> TryProcessOrder(bool processRetry, CancellationToken cancellationToken = default)
    {
        using Activity? activity = _activitySource.StartActivity("TryProcessOrder")?.SetTag("Retry", processRetry);
        string savepoint = "after_read_with_lock";
        UnitOfWork unitOfWork;
        try
        {
            unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(e, "Failed to start a unit of work for past due order processing.");
            }

            return false;
        }

        NotificationOrder? pastDueOrder = null;
        try
        {
            pastDueOrder = await orderRepository.GetNextPastDueOrder(unitOfWork, processRetry, cancellationToken);
            activity?.SetTag("Count", pastDueOrder == null ? 0 : 1);
            if (pastDueOrder == null)
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            await unitOfWorkRepository.SaveUnitOfWork(unitOfWork, savepoint);
            await ProcessOrder(pastDueOrder, unitOfWork);
            await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

            return true;
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(e, "An error occurred while processing past due order {OrderId}: {ErrorMessage}", pastDueOrder?.Id, e.Message);
            }

            if (unitOfWork.Connection?.State != System.Data.ConnectionState.Open)
            {
                return false;
            }

            if (pastDueOrder != null)
            {
                activity?.SetTag("OrderId", pastDueOrder.Id);
                try
                {
                    await unitOfWorkRepository.RollbackUnitOfWorkToSavepoint(unitOfWork, savepoint);
                    await orderRepository.SetRetryStatus(unitOfWork, pastDueOrder, $"{e} {e.Message}");
                    await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
                }
                catch (Exception)
                {
                    await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                }
            }
            else
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
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
                if (sendingConditionEvaluationResult.IsSendConditionMet == false)
                {
                    await orderRepository.SetOrderSendConditionNotMetAsync(unitOfWork, order);
                }
                else
                {
                    await orderRepository.SetRetryStatus(unitOfWork, order, "Send condition evaluation inconclusive");
                }

                break;

            case { IsSendConditionMet: true }:
                SmsOrderProcessingResult smsOrderProcessingResult = new([], null);
                EmailOrderProcessingResult emailOrderProcessingResult = new([], null);

                switch (order.NotificationChannel)
                {
                    case NotificationChannel.Sms:
                        var smsResult = await smsProcessingService.ProcessOrder(order);
                        smsOrderProcessingResult = smsResult;
                        break;

                    case NotificationChannel.Email:
                        var emailResult = await emailProcessingService.ProcessOrder(order);
                        emailOrderProcessingResult = emailResult;
                        break;

                    case NotificationChannel.EmailAndSms:
                        var emailAndSmsResult = await emailAndSmsProcessingService.ProcessOrderAsync(order);
                        emailOrderProcessingResult = emailAndSmsResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = emailAndSmsResult.SmsOrderProcessingResult;
                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        var preferredResult = await preferredChannelProcessingService.ProcessOrder(order);
                        emailOrderProcessingResult = preferredResult.EmailOrderProcessingResult;
                        smsOrderProcessingResult = preferredResult.SmsOrderProcessingResult;
                        break;
                }

                await orderRepository.PersistProcessingResultAsync(unitOfWork, order, emailOrderProcessingResult, smsOrderProcessingResult);
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

        var evaluationResult = await conditionClient.CheckSendCondition(order.ConditionEndpoint);

        if (evaluationResult.IsSuccess)
        {
            if (evaluationResult.Value)
            {
                logger.LogTrace(
                    "// OrderProcessingService // IsSendConditionMet // Condition check yield true for order '{OrderId}' at endpoint '{Endpoint}'.",
                    order.Id,
                    order.ConditionEndpoint);
            }
            else
            {
                logger.LogInformation(
                    "// OrderProcessingService // IsSendConditionMet // Condition check yield false for order '{OrderId}' at endpoint '{Endpoint}'.",
                    order.Id,
                    order.ConditionEndpoint);
            }

            return new SendConditionEvaluationResult { IsSendConditionMet = evaluationResult.Value };
        }

        if (order.OrderProcessingStatus == OrderProcessingStatus.Retrying)
        {
            logger.LogInformation(
                "// OrderProcessingService // IsSendConditionMet // Condition check failed on retry for order with ID '{OrderId}' at endpoint '{Endpoint}'. Status code: {StatusCode}. Error message: '{ErrorMessage}'. Processing the order regardless.",
                order.Id,
                order.ConditionEndpoint,
                evaluationResult.Error!.StatusCode,
                evaluationResult.Error.Message ?? "No error message provided");

            return new SendConditionEvaluationResult { IsSendConditionMet = true };
        }
        else
        {
            logger.LogInformation(
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
