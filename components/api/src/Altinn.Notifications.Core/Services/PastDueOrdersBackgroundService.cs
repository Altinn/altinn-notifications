using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that triggers the past due order processing loop in the <see cref="IOrderProcessingService"/>.
/// </summary>
public class PastDueOrdersBackgroundService : BackgroundService
{
    private readonly ILogger<PastDueOrdersBackgroundService> _logger;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly NotificationConfig _config;
    private readonly ManyOrdersLately _manyPastDueOrdersLately = new();
    private readonly ManyOrdersLately _manyRetryOrdersLately = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersBackgroundService"/> class.
    /// </summary>
    public PastDueOrdersBackgroundService(IOrderProcessingService orderProcessingService, IOptions<NotificationConfig> config, ILogger<PastDueOrdersBackgroundService> logger)
    {
        _orderProcessingService = orderProcessingService;
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.PastDueOrdersTaskCount == 0 && _config.RetryOrdersTaskCount == 0)
        {
            // Unit test case: No tasks configured, so we don't start any loops. This is useful for unit tests that don't want to start background processing.
            return;
        }

        // TODO pastdue poc: How to suspend and resume processing
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Task[] pastDueTasks = Enumerable.Range(0, _config.PastDueOrdersTaskCount)
                    .Select(i => RunOrderLoop(
                        processRetry: false,
                        isFirstInstance: i == 0,
                        stoppingToken))
                    .ToArray();

                Task[] retryTasks = Enumerable.Range(0, _config.RetryOrdersTaskCount)
                    .Select(i => RunOrderLoop(
                        processRetry: true,
                        isFirstInstance: i == 0,
                        stoppingToken))
                    .ToArray();

                await Task.WhenAll(pastDueTasks.Concat(retryTasks));
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

    private async Task RunOrderLoop(bool processRetry, bool isFirstInstance, CancellationToken stoppingToken)
    {
        int consecutiveRunsWithOrderReturned = 0;
        var idleDelay = GetDelay(processRetry, isFirstInstance);
        int rampupLimit = processRetry ? _config.RetryOrdersRampUpLimit : _config.PastDueOrdersRampUpLimit;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool shouldAttemptProcessing = isFirstInstance
                    || (processRetry ? _manyRetryOrdersLately.Get() : _manyPastDueOrdersLately.Get());
                bool shouldDelay = false;
                if (shouldAttemptProcessing)
                {
                    bool gotOrder = await _orderProcessingService.StartProcessingPastDueOrders(processRetry, stoppingToken);
                    if (!gotOrder && !stoppingToken.IsCancellationRequested)
                    {
                        consecutiveRunsWithOrderReturned = 0;
                        shouldDelay = true;
                    }
                    else
                    {
                        consecutiveRunsWithOrderReturned++;
                        if (consecutiveRunsWithOrderReturned >= rampupLimit)
                        {
                            if (processRetry)
                            {
                                _manyRetryOrdersLately.Set(true);
                            }
                            else
                            {
                                _manyPastDueOrdersLately.Set(true);
                            }
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
                _logger.LogError(e, "Unhandled error in past due order loop. processRetry: {ProcessRetry}", processRetry);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
        }
    }
    
    private TimeSpan GetDelay(bool processRetry, bool isFirstInstance)
    {
        if (isFirstInstance)
        {
            return TimeSpan.FromSeconds(
                processRetry ? _config.RetryOrdersPrimaryTaskIdleDelaySeconds : _config.PastDueOrdersPrimaryTaskIdleDelaySeconds);
        }
        else
        {
            return TimeSpan.FromSeconds(
                processRetry ? _config.RetryOrdersAdditionalTasksIdleDelaySeconds : _config.PastDueOrdersAdditionalTasksIdleDelaySeconds);
        }
    }

    /// <summary>
    /// A thread-safe flag indicating whether there have been many orders processed lately.
    /// </summary>
    internal class ManyOrdersLately
    {
        private bool _manyOrdersLately;
        private readonly object _lock = new();

        /// <summary>
        /// Gets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <returns></returns>
        public bool Get()
        {
            lock (_lock)
            {
                return _manyOrdersLately;
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
                _manyOrdersLately = value;
            }
        }
    }
}
