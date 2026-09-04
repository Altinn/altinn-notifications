using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that triggers the past due order processing loop in the <see cref="IOrderProcessingService"/>.
/// </summary>
public class PastDueOrdersBackgroundService : BackgroundService
{
    private readonly ILogger<PastDueOrdersBackgroundService> _logger;
    private readonly IOrderProcessingService _orderProcessingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastDueOrdersBackgroundService"/> class.
    /// </summary>
    public PastDueOrdersBackgroundService(IOrderProcessingService orderProcessingService, ILogger<PastDueOrdersBackgroundService> logger)
    {
        _orderProcessingService = orderProcessingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO pastdue poc: How to suspend and resume processing
        try
        {
            while (true)
            {
                await _orderProcessingService.StartProcessingPastDueOrders(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }
}
