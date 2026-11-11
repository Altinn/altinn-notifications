using System.Diagnostics;
using Altinn.Notifications.Core.Enums;
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
    ILogger<OrderProcessingService> logger,
    ISmsOrderProcessingService smsProcessor,
    IEmailOrderProcessingService emailProcessor,
    IPreferredChannelProcessingService preferredProcessor,
    IEmailAndSmsOrderProcessingService emailAndSmsProcessor,
    IUnitOfWorkRepository uowRepo) : IOrderProcessingService
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
            unitOfWork = await uowRepo.StartUnitOfWork();
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
                await uowRepo.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            await uowRepo.SaveUnitOfWork(unitOfWork, savepoint);

            SmsOrderProcessingResult smsOrderProcessingResult = new([], null);
            EmailOrderProcessingResult emailOrderProcessingResult = new([], null);

            switch (pastDueOrder.NotificationChannel)
            {
                case NotificationChannel.Sms:
                    smsOrderProcessingResult = await smsProcessor.ProcessOrder(pastDueOrder);
                    break;

                case NotificationChannel.Email:
                    emailOrderProcessingResult = await emailProcessor.ProcessOrder(pastDueOrder);
                    break;

                case NotificationChannel.EmailAndSms:
                    var emailAndSmsResult = await emailAndSmsProcessor.ProcessOrderAsync(pastDueOrder);
                    emailOrderProcessingResult = emailAndSmsResult.EmailOrderProcessingResult;
                    smsOrderProcessingResult = emailAndSmsResult.SmsOrderProcessingResult;
                    break;

                case NotificationChannel.SmsPreferred:
                case NotificationChannel.EmailPreferred:
                    var preferredResult = await preferredProcessor.ProcessOrder(pastDueOrder);
                    emailOrderProcessingResult = preferredResult.EmailOrderProcessingResult;
                    smsOrderProcessingResult = preferredResult.SmsOrderProcessingResult;
                    break;
            }

            await orderRepository.PersistProcessingResultAsync(unitOfWork, pastDueOrder, emailOrderProcessingResult, smsOrderProcessingResult);
            await uowRepo.CommitUnitOfWork(unitOfWork);

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
                    await uowRepo.RollbackUnitOfWorkToSavepoint(unitOfWork, savepoint);
                    await orderRepository.SetRetryStatus(unitOfWork, pastDueOrder.Id, $"{e} {e.Message}");
                    await uowRepo.CommitUnitOfWork(unitOfWork);
                }
                catch (Exception)
                {
                    await uowRepo.RollbackUnitOfWork(unitOfWork);
                }
            }
            else
            {
                await uowRepo.RollbackUnitOfWork(unitOfWork);
            }

            return false;
        }
    }
}
