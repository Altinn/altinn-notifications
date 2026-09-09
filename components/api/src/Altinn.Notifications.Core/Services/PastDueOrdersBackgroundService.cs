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
            catch (Exception)
            {
            }
        }
    }
}
