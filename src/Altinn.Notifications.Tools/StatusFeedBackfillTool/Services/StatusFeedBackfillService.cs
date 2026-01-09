using System.Diagnostics;
using System.Text.Json;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StatusFeedBackfillTool.Configuration;

namespace StatusFeedBackfillTool.Services;

/// <summary>
/// Service responsible for backfilling missing status feed entries for orders.
/// Reads order IDs from a file and inserts missing status feed entries.
/// </summary>
public class StatusFeedBackfillService
{
    private readonly OrderRepository _orderRepository;
    private readonly BackfillSettings _settings;
    private readonly ILogger<StatusFeedBackfillService> _logger;

    public StatusFeedBackfillService(
        OrderRepository orderRepository,
        IOptions<BackfillSettings> settings,
        ILogger<StatusFeedBackfillService> logger)
    {
        _orderRepository = orderRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var settings = $"=== BACKFILL MODE ===\n\n" +
            $"Tool Settings:\n" +
            $"  Mode: Backfill\n" +
            $"  Order IDs File: {_settings.OrderIdsFilePath}\n" +
            $"  Dry Run: {_settings.DryRun}\n";
        
        _logger.LogInformation("{Settings}", settings);

        var ordersToProcess = await LoadOrdersFromFile(cancellationToken);

        if (ordersToProcess.Count == 0)
        {
            stopwatch.Stop();
            _logger.LogInformation("No orders found in file: {FilePath}\nTotal elapsed time: {Elapsed:hh\\:mm\\:ss}", 
                _settings.OrderIdsFilePath, stopwatch.Elapsed);
            return;
        }

        _logger.LogInformation("Loaded {Count} orders from file\n", ordersToProcess.Count);

        var (totalProcessed, totalInserted, totalErrors) = await ProcessOrders(ordersToProcess, cancellationToken);

        stopwatch.Stop();
        LogBackfillSummary(totalProcessed, totalInserted, totalErrors, stopwatch.Elapsed);
    }

    private async Task<List<Guid>> LoadOrdersFromFile(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settings.OrderIdsFilePath))
        {
            _logger.LogError("File not found: {FilePath}", _settings.OrderIdsFilePath);
            return [];
        }

        var json = await File.ReadAllTextAsync(_settings.OrderIdsFilePath, cancellationToken);
        var orders = JsonSerializer.Deserialize<List<Guid>>(json);
        return orders ?? [];
    }

    private async Task<(int totalProcessed, int totalInserted, int totalErrors)> ProcessOrders(List<Guid> allOrders, CancellationToken cancellationToken)
    {
        int totalProcessed = 0;
        int totalInserted = 0;
        int totalErrors = 0;
        int currentOrder = 0;
        int totalOrderCount = allOrders.Count;

        foreach (var orderId in allOrders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested. Stopping processing.");
                break;
            }

            currentOrder++;

            if (currentOrder % 10 == 0 || currentOrder == 1)
            {
                _logger.LogInformation("Processing order {Current}/{Total}...", currentOrder, totalOrderCount);
            }

            try
            {
                if (_settings.DryRun)
                {
                    // In dry run mode, just count what would be inserted
                    totalInserted++;
                }
                else
                {
                    // Actually insert the status feed entry
                    await _orderRepository.InsertStatusFeedForOrder(orderId);
                    totalInserted++;
                }
                totalProcessed++;
            }
            catch (Exception ex)
            {
                totalErrors++;
                _logger.LogError(ex, "Error processing order {OrderId}: {Message}", orderId, ex.Message);
            }
        }

        return (totalProcessed, totalInserted, totalErrors);
    }

    private void LogBackfillSummary(int totalProcessed, int totalInserted, int totalErrors, TimeSpan elapsed)
    {
        var action = _settings.DryRun ? "Would Be Inserted" : "Inserted";
        var summary = $"\n========================================\n" +
            $"Backfill Summary\n" +
            $"========================================\n" +
            $"Total Orders Processed: {totalProcessed}\n" +
            $"Total Status Feed Entries {action}: {totalInserted}\n" +
            $"Total Errors: {totalErrors}\n" +
            $"Total elapsed time: {elapsed:hh\\:mm\\:ss}";
        
        _logger.LogInformation("{Summary}", summary);
        
        if (_settings.DryRun)
        {
            _logger.LogWarning("DRY RUN MODE - No changes were committed to the database");
        }
    }
}
