using System.Diagnostics;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
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
    ILogger<OrderProcessingService> logger,
    IUnitOfWorkRepository unitOfWorkRepository) : IOrderProcessingService
{
    private static readonly ActivitySource _activitySource = new(BackgroundActivitySource.Name);

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
                    await orderRepository.SetRetryStatus(unitOfWork, pastDueOrder.Id, $"{e} {e.Message}");
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
        SmsOrderProcessingResult smsOrderProcessingResult = new([], null);
        EmailOrderProcessingResult emailOrderProcessingResult = new([], null);

        switch (order.NotificationChannel)
        {
            case NotificationChannel.Sms:
                smsOrderProcessingResult = await smsProcessingService.ProcessOrder(order);
                break;

            case NotificationChannel.Email:
                emailOrderProcessingResult = await emailProcessingService.ProcessOrder(order);
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
    }
}
