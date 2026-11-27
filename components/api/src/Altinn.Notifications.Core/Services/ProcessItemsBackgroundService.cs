using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that processes different notification and order types in separate loops, adjusting worker counts as configuration changes.
/// </summary>
/// <remarks>
/// Reconciles the worker counts with the latest configuration snapshot every minute.
/// </remarks>
public class ProcessItemsBackgroundService : BackgroundService
{
    private static readonly TimeSpan ConfigRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IEmailNotificationService _emailSendingService;
    private readonly ISmsNotificationService _smsSendingService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOptionsMonitor<NotificationConfig> _config;
    private readonly ILogger<ProcessItemsBackgroundService> _logger;
    private readonly INotificationScheduleService _notificationScheduleService;
    private readonly IDynamicConfigService _dynamicConfigService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ManyItemsLately _manyEmailDaytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyEmailAnytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyComposedEmailNotificationsLately = new();
    private readonly ManyItemsLately _manySmsDaytimeNotificationsLately = new();
    private readonly ManyItemsLately _manySmsAnytimeNotificationsLately = new();
    private readonly ManyItemsLately _manyPastDueOrdersLately = new();
    private readonly ManyItemsLately _manyRetryOrdersLately = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessItemsBackgroundService"/> class.
    /// </summary>
    public ProcessItemsBackgroundService(
        IEmailNotificationService emailSendingService,
        ISmsNotificationService smsSendingService,
        IOrderProcessingService orderProcessingService,
        IOptionsMonitor<NotificationConfig> config,
        ILogger<ProcessItemsBackgroundService> logger,
        INotificationScheduleService notificationScheduleService,
        IDynamicConfigService dynamicConfigService,
        IHostApplicationLifetime applicationLifetime)
    {
        _applicationLifetime = applicationLifetime;
        _emailSendingService = emailSendingService;
        _smsSendingService = smsSendingService;
        _orderProcessingService = orderProcessingService;
        _config = config;
        _logger = logger;
        _notificationScheduleService = notificationScheduleService;
        _dynamicConfigService = dynamicConfigService;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stop claiming new items as soon as the host begins shutting down, before any hosted service is stopped.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _applicationLifetime.ApplicationStopping);
        await RunLoopsAsync(linkedCts.Token);
    }

    private async Task RunLoopsAsync(CancellationToken stoppingToken)
    {
        List<WorkerPool> workerPools = CreateWorkerPools();
        try
        {
            stoppingToken.ThrowIfCancellationRequested();
            NotificationConfig staticConfig = _config.CurrentValue;
            if (!TryValidateTaskCounts(staticConfig))
            {
                throw new InvalidOperationException("Notification process loop task counts must be non-negative.");
            }

            using var timer = new PeriodicTimer(ConfigRefreshInterval);
            do
            {
                if (await GetDynamicConfig(staticConfig, stoppingToken) is { } refreshed)
                {
                    await ReconcileWorkerPools(workerPools, refreshed.Config, refreshed.PodCount, stoppingToken);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Process item background service is stopping.");
        }
        finally
        {
            await Task.WhenAll(workerPools.Select(pool => pool.StopAsync()));
        }
    }

    private List<WorkerPool> CreateWorkerPools()
    {
        return
        [
            new(
                config => config.EmailDaytimeNotificationsProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manyEmailDaytimeNotificationsLately,
                    token => _emailSendingService.SendNotification(SendingTimePolicy.Daytime, token),
                    _notificationScheduleService.CanSendEmailNow,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.EmailAnytimeNotificationsProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manyEmailAnytimeNotificationsLately,
                    token => _emailSendingService.SendNotification(SendingTimePolicy.Anytime, token),
                    null,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.ComposedEmailNotificationsProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manyComposedEmailNotificationsLately,
                    _emailSendingService.SendComposedNotification,
                    null,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.SmsDaytimeNotificationsProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manySmsDaytimeNotificationsLately,
                    token => _smsSendingService.SendNotification(SendingTimePolicy.Daytime, token),
                    _notificationScheduleService.CanSendSmsNow,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.SmsAnytimeNotificationsProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manySmsAnytimeNotificationsLately,
                    token => _smsSendingService.SendNotification(SendingTimePolicy.Anytime, token),
                    null,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.PastDueOrdersProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manyPastDueOrdersLately,
                    token => _orderProcessingService.TryProcessOrder(processRetry: false, token),
                    null,
                    i == 0,
                    loopConfig,
                    ct)),
            new(
                config => config.RetryOrdersProcessLoopConfig,
                (i, loopConfig, ct) => RunProcessItemLoop(
                    _manyRetryOrdersLately,
                    token => _orderProcessingService.TryProcessOrder(processRetry: true, token),
                    null,
                    i == 0,
                    loopConfig,
                    ct))
        ];
    }

    private static async Task ReconcileWorkerPools(List<WorkerPool> workerPools, NotificationConfig config, int podCount, CancellationToken stoppingToken)
    {
        foreach (WorkerPool pool in workerPools)
        {
            await pool.ReconcileAsync(config, podCount, stoppingToken);
        }
    }

    private bool TryValidateTaskCounts(NotificationConfig config)
    {
        (string Name, int Count)[] taskCounts =
        [
            (nameof(config.EmailDaytimeNotificationsProcessLoopConfig), config.EmailDaytimeNotificationsProcessLoopConfig.TaskCount),
            (nameof(config.EmailAnytimeNotificationsProcessLoopConfig), config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount),
            (nameof(config.ComposedEmailNotificationsProcessLoopConfig), config.ComposedEmailNotificationsProcessLoopConfig.TaskCount),
            (nameof(config.SmsDaytimeNotificationsProcessLoopConfig), config.SmsDaytimeNotificationsProcessLoopConfig.TaskCount),
            (nameof(config.SmsAnytimeNotificationsProcessLoopConfig), config.SmsAnytimeNotificationsProcessLoopConfig.TaskCount),
            (nameof(config.PastDueOrdersProcessLoopConfig), config.PastDueOrdersProcessLoopConfig.TaskCount),
            (nameof(config.RetryOrdersProcessLoopConfig), config.RetryOrdersProcessLoopConfig.TaskCount)
        ];

        foreach ((string name, int count) in taskCounts)
        {
            if (count < 0)
            {
                _logger.LogError("Ignoring process loop configuration because {ProcessLoop} has a negative TaskCount: {TaskCount}", name, count);
                return false;
            }
        }

        return true;
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
                _logger.LogError(e, "Unhandled error in processing loop: {MethodName}", processItem.Method.Name);

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

    private async Task<(NotificationConfig Config, int PodCount)?> GetDynamicConfig(NotificationConfig staticConfig, CancellationToken stoppingToken)
    {
        try
        {
            return await _dynamicConfigService.GetDynamicConfig(staticConfig, ConfigRefreshInterval, stoppingToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            stoppingToken.ThrowIfCancellationRequested();
            _logger.LogError(exception, "Could not refresh dynamic processing configuration. Keeping the current workers until the next refresh.");
            return null;
        }
    }

    /// <summary>
    /// Manages the workers for a single processing loop.
    /// </summary>
    internal sealed class WorkerPool(
        Func<NotificationConfig, ProcessLoopConfig> getLoopConfig,
        Func<int, ProcessLoopConfig, CancellationToken, Task> runWorker)
    {
        private readonly List<Worker> _workers = [];

        /// <summary>
        /// Adjusts the pool to the configured per-pod worker count.
        /// </summary>
        public async Task ReconcileAsync(NotificationConfig config, int podCount, CancellationToken stoppingToken)
        {
            ProcessLoopConfig loopConfig = getLoopConfig(config);
            int desiredTaskCount = loopConfig.TaskCount / podCount;
            while (_workers.Count < desiredTaskCount)
            {
                stoppingToken.ThrowIfCancellationRequested();
                int workerIndex = _workers.Count;
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                Task task = runWorker(workerIndex, loopConfig, cancellationTokenSource.Token);
                _workers.Add(new Worker(cancellationTokenSource, task));
            }

            while (_workers.Count > desiredTaskCount)
            {
                Worker worker = _workers[^1];
                _workers.RemoveAt(_workers.Count - 1);
                await StopWorkerAsync(worker);
            }
        }

        /// <summary>
        /// Stops all workers in the pool.
        /// </summary>
        public async Task StopAsync()
        {
            while (_workers.Count > 0)
            {
                Worker worker = _workers[^1];
                _workers.RemoveAt(_workers.Count - 1);
                await StopWorkerAsync(worker);
            }
        }

        private static async Task StopWorkerAsync(Worker worker)
        {
            await worker.CancellationTokenSource.CancelAsync();
            try
            {
                await worker.Task;
            }
            catch (OperationCanceledException exception) when (
                worker.CancellationTokenSource.IsCancellationRequested &&
                exception.CancellationToken == worker.CancellationTokenSource.Token)
            {
                // The worker observed the cancellation we requested.
            }
            finally
            {
                worker.CancellationTokenSource.Dispose();
            }
        }

        private sealed record Worker(CancellationTokenSource CancellationTokenSource, Task Task);
    }

    /// <summary>
    /// A thread-safe flag indicating whether there have been many items processed lately.
    /// </summary>
    internal class ManyItemsLately
    {
        private bool _manyItemsLately;
        private readonly Lock _lock = new();

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
