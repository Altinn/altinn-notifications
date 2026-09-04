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
                // This loop will normally only get executed once, but in case of an exception we want to restart
                Task[] tasks = Enumerable.Range(0, _config.PastDueOrdersTaskCount)
                    .Select(_ => _orderProcessingService.StartProcessingPastDueOrders(stoppingToken))
                    .ToArray();

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); // Wait before restarting
            }
        }
    }
}
