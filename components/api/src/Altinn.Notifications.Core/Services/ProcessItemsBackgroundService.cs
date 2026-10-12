using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that processes items with different shapes (e.g. email notifications, composed email notifications,
/// SMS daytime notifications, and SMS anytime notifications) in separate loops.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProcessItemsBackgroundService"/> class.
/// </remarks>
public class ProcessItemsBackgroundService(
    IEmailNotificationService emailSendingService,
    ISmsNotificationService smsSendingService,
    IOrderProcessingService orderProcessingService,
    IOptions<NotificationConfig> config,
    ILogger<ProcessItemsBackgroundService> logger,
    INotificationScheduleService notificationScheduleService)
    : BackgroundService
{
    private readonly NotificationConfig _config = config.Value;
    private readonly ManyItemsLately _manyEmailNotificationsLately = new();
    private readonly ManyItemsLately _manyComposedEmailNotificationsLately = new();
    private readonly ManyItemsLately _manySmsDaytimeNotificationsLately = new();
    private readonly ManyItemsLately _manySmsAnytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyPastDueOrdersLately = new();
    private readonly ManyItemsLately _manyRetryOrdersLately = new();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int totalTasks = _config.EmailNotificationsProcessLoopConfig.TaskCount
            + _config.ComposedEmailNotificationsProcessLoopConfig.TaskCount
            + _config.SmsDaytimeNotificationsProcessLoopConfig.TaskCount
            + _config.SmsAnytimeNotificationsProcessLoopConfig.TaskCount
            + _config.PastDueOrdersProcessLoopConfig.TaskCount
            + _config.RetryOrdersProcessLoopConfig.TaskCount;
        if (totalTasks <= 0)
        {
            // Unit test case: No tasks configured, so we don't start any loops. This is useful for unit tests that don't want to start background processing.
            // Could also be used in production to disable email notification processing if needed.
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Task[] emailNotificationTasks = [.. Enumerable.Range(0, _config.EmailNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyEmailNotificationsLately,
                        emailSendingService.SendNotification,
                        null,
                        isFirstInstance: i == 0,
                        _config.EmailNotificationsProcessLoopConfig,
                        stoppingToken))];

                Task[] composedEmailNotificationTask = [.. Enumerable.Range(0, _config.ComposedEmailNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyComposedEmailNotificationsLately,
                        emailSendingService.SendComposedNotification,
                        null,
                        isFirstInstance: i == 0,
                        _config.ComposedEmailNotificationsProcessLoopConfig,
                        stoppingToken))];

                Task[] smsDaytimeNotificationTasks = [.. Enumerable.Range(0, _config.SmsDaytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manySmsDaytimeNotificationsLately,
                        ct => smsSendingService.SendNotification(SendingTimePolicy.Daytime, ct),
                        notificationScheduleService.CanSendSmsNow,
                        isFirstInstance: i == 0,
                        _config.SmsDaytimeNotificationsProcessLoopConfig,
                        stoppingToken))];

                Task[] smsAnytimeNotificationTasks = [.. Enumerable.Range(0, _config.SmsAnytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manySmsAnytimeNotificationsLately,
                        ct => smsSendingService.SendNotification(SendingTimePolicy.Anytime, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.SmsAnytimeNotificationsProcessLoopConfig,
                        stoppingToken))];

                Task[] pastDueOrdersTasks = [.. Enumerable.Range(0, _config.PastDueOrdersProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyPastDueOrdersLately,
                        ct => orderProcessingService.TryProcessOrder(processRetry:false, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.PastDueOrdersProcessLoopConfig,
                        stoppingToken))];

                Task[] retryOrdersTasks = [.. Enumerable.Range(0, _config.RetryOrdersProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyRetryOrdersLately,
                        ct => orderProcessingService.TryProcessOrder(processRetry: true, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.RetryOrdersProcessLoopConfig,
                        stoppingToken))];

                await Task.WhenAll(emailNotificationTasks
                    .Concat(composedEmailNotificationTask)
                    .Concat(smsDaytimeNotificationTasks)
                    .Concat(smsAnytimeNotificationTasks)
                    .Concat(pastDueOrdersTasks)
                    .Concat(retryOrdersTasks));
            }
            catch (Exception)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); // Wait before restarting
            }
        }
    }

    private async Task RunProcessItemLoop(
        ManyItemsLately manyItemsLately,
        Func<CancellationToken, Task<bool>> processItem,
        Func<bool>? processEnabled,
        bool isFirstInstance,
        ProcessLoopConfig config,
        CancellationToken stoppingToken)
    {
        int consecutiveRunsWithItemReturned = 0;
        var idleDelay = GetDelay(isFirstInstance, config);
        int rampupLimit = config.RampUpLimit;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool shouldAttemptProcessing = (isFirstInstance || manyItemsLately.Get()) && (processEnabled == null || processEnabled());
                bool shouldDelay = false;
                if (shouldAttemptProcessing)
                {
                    bool gotItem = await processItem(stoppingToken);
                    if (!gotItem && !stoppingToken.IsCancellationRequested)
                    {
                        consecutiveRunsWithItemReturned = 0;
                        shouldDelay = true;
                        manyItemsLately.Set(false);
                    }
                    else
                    {
                        consecutiveRunsWithItemReturned++;
                        if (consecutiveRunsWithItemReturned >= rampupLimit)
                        {
                            manyItemsLately.Set(true);
                        }
                    }
                }
                else
                {
                    shouldDelay = true;
                }

                if (shouldDelay && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled error in processing loop: {MethodName}", processItem.Method.Name);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
        }
    }

    private static TimeSpan GetDelay(bool isFirstInstance, ProcessLoopConfig config)
    {
        return TimeSpan.FromSeconds(isFirstInstance ? config.PrimaryTaskIdleDelaySeconds : config.AdditionalTasksIdleDelaySeconds);
    }

    /// <summary>
    /// A thread-safe flag indicating whether there have been many items processed lately.
    /// </summary>
    internal class ManyItemsLately
    {
        private bool _manyItemsLately;
        private readonly object _lock = new();

        /// <summary>
        /// Gets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <returns></returns>
        public bool Get()
        {
            lock (_lock)
            {
                return _manyItemsLately;
            }
        }

        /// <summary>
        /// Sets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <param name="value">The value to set the flag to.</param>
        public void Set(bool value)
        {
            lock (_lock)
            {
                _manyItemsLately = value;
            }
        }
    }
}
