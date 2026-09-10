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
                    .Select(_ => RunOrderLoop(false, stoppingToken))
                    .ToArray();
                Task[] retryTasks = Enumerable.Range(0, _config.RetryOrdersTaskCount)
                    .Select(_ => RunOrderLoop(true, stoppingToken))
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

    private async Task RunOrderLoop(bool processRetry, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await _orderProcessingService.StartProcessingPastDueOrders(processRetry, stoppingToken) && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay((processRetry ? _config.RetryOrdersIdleDelaySeconds : _config.PastDueOrdersIdleDelaySeconds) * 1000, stoppingToken);
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
                    await Task.Delay((processRetry ? _config.RetryOrdersIdleDelaySeconds : _config.PastDueOrdersIdleDelaySeconds) * 1000, stoppingToken);
                }
            }
        }
    }
}
