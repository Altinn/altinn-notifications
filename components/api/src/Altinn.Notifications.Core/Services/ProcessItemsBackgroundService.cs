using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that processes items with different shapes (e.g. email daytime notifications, email anytime notifications,
/// composed email notifications, SMS daytime notifications, and SMS anytime notifications) in separate loops.
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
    INotificationScheduleService notificationScheduleService,
    IHostApplicationLifetime applicationLifetime)
    : BackgroundService
{
    private readonly NotificationConfig _config = config.Value;
    private readonly ManyItemsLately _manyEmailDaytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyEmailAnytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyComposedEmailNotificationsLately = new();
    private readonly ManyItemsLately _manySmsDaytimeNotificationsLately = new();
    private readonly ManyItemsLately _manySmsAnytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyPastDueOrdersLately = new();
    private readonly ManyItemsLately _manyRetryOrdersLately = new();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ApplicationStopping fires at SIGTERM, before hosted services are stopped in reverse registration order.
        // Linking it makes the loops stop claiming new items immediately instead of when this service is stopped.
        using var shutdownSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, applicationLifetime.ApplicationStopping);
        CancellationToken shutdownToken = shutdownSource.Token;

        int totalTasks = _config.EmailDaytimeNotificationsProcessLoopConfig.TaskCount
            + _config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount
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

        while (!shutdownToken.IsCancellationRequested)
        {
            try
            {
                Task[] emailDaytimeNotificationTasks = [.. Enumerable.Range(0, _config.EmailDaytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyEmailDaytimeNotificationsLately,
                        ct => emailSendingService.SendNotification(SendingTimePolicy.Daytime, ct),
                        notificationScheduleService.CanSendEmailNow,
                        isFirstInstance: i == 0,
                        _config.EmailDaytimeNotificationsProcessLoopConfig,
                        shutdownToken))];

                Task[] emailAnytimeNotificationTasks = [.. Enumerable.Range(0, _config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyEmailAnytimeNotificationsLately,
                        ct => emailSendingService.SendNotification(SendingTimePolicy.Anytime, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.EmailAnytimeNotificationsProcessLoopConfig,
                        shutdownToken))];

                Task[] composedEmailNotificationTask = [.. Enumerable.Range(0, _config.ComposedEmailNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyComposedEmailNotificationsLately,
                        emailSendingService.SendComposedNotification,
                        null,
                        isFirstInstance: i == 0,
                        _config.ComposedEmailNotificationsProcessLoopConfig,
                        shutdownToken))];

                Task[] smsDaytimeNotificationTasks = [.. Enumerable.Range(0, _config.SmsDaytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manySmsDaytimeNotificationsLately,
                        ct => smsSendingService.SendNotification(SendingTimePolicy.Daytime, ct),
                        notificationScheduleService.CanSendSmsNow,
                        isFirstInstance: i == 0,
                        _config.SmsDaytimeNotificationsProcessLoopConfig,
                        shutdownToken))];

                Task[] smsAnytimeNotificationTasks = [.. Enumerable.Range(0, _config.SmsAnytimeNotificationsProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manySmsAnytimeNotificationsLately,
                        ct => smsSendingService.SendNotification(SendingTimePolicy.Anytime, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.SmsAnytimeNotificationsProcessLoopConfig,
                        shutdownToken))];

                Task[] pastDueOrdersTasks = [.. Enumerable.Range(0, _config.PastDueOrdersProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyPastDueOrdersLately,
                        ct => orderProcessingService.TryProcessOrder(processRetry:false, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.PastDueOrdersProcessLoopConfig,
                        shutdownToken))];

                Task[] retryOrdersTasks = [.. Enumerable.Range(0, _config.RetryOrdersProcessLoopConfig.TaskCount)
                    .Select(i => RunProcessItemLoop(
                        _manyRetryOrdersLately,
                        ct => orderProcessingService.TryProcessOrder(processRetry: true, ct),
                        null,
                        isFirstInstance: i == 0,
                        _config.RetryOrdersProcessLoopConfig,
                        shutdownToken))];

                await Task.WhenAll(emailDaytimeNotificationTasks
                    .Concat(emailAnytimeNotificationTasks)
                    .Concat(composedEmailNotificationTask)
                    .Concat(smsDaytimeNotificationTasks)
                    .Concat(smsAnytimeNotificationTasks)
                    .Concat(pastDueOrdersTasks)
                    .Concat(retryOrdersTasks));
            }
            catch (Exception)
            {
                if (shutdownToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), shutdownToken); // Wait before restarting
                }
                catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
                {
                    return;
                }
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
                    // In-flight work is not cancelled on shutdown so it can finish; the host's shutdown timeout bounds how long it may take.
                    bool gotItem = await processItem(CancellationToken.None);
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
