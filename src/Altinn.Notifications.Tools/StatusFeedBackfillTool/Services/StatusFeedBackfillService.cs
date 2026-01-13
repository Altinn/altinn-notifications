using System.Diagnostics;
using System.Text.Json;
using Altinn.Notifications.Persistence.Repository;
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

    public StatusFeedBackfillService(
        OrderRepository orderRepository,
        IOptions<BackfillSettings> settings)
    {
        _orderRepository = orderRepository;
        _settings = settings.Value;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine("=== BACKFILL MODE ===\n");
        Console.WriteLine("Tool Settings:");
        Console.WriteLine($"  Mode: Backfill");
        Console.WriteLine($"  Order IDs File: {_settings.OrderIdsFilePath}");
        Console.WriteLine($"  Dry Run: {_settings.DryRun}");
        Console.WriteLine();

        var ordersToProcess = await LoadOrdersFromFile(cancellationToken);

        if (ordersToProcess.Count == 0)
        {
            stopwatch.Stop();
            Console.WriteLine($"No orders found in file: {_settings.OrderIdsFilePath}");
            Console.WriteLine($"Total elapsed time: {stopwatch.Elapsed:hh\\:mm\\:ss}");
            return;
        }

        Console.WriteLine($"Loaded {ordersToProcess.Count} orders from file\n");

        var (totalProcessed, totalInserted, totalErrors) = await ProcessOrders(ordersToProcess, cancellationToken);

        stopwatch.Stop();
        LogBackfillSummary(totalProcessed, totalInserted, totalErrors, stopwatch.Elapsed);
    }

    private async Task<List<Guid>> LoadOrdersFromFile(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settings.OrderIdsFilePath))
        {
            Console.WriteLine($"ERROR: File not found: {_settings.OrderIdsFilePath}");
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
                Console.WriteLine("WARNING: Cancellation requested. Stopping processing.");
                break;
            }

            currentOrder++;

            if (currentOrder % 10 == 0 || currentOrder == 1)
            {
                Console.WriteLine($"Processing order {currentOrder}/{totalOrderCount}...");
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
                Console.WriteLine($"ERROR processing order {orderId}: {ex.Message}");
            }
        }

        return (totalProcessed, totalInserted, totalErrors);
    }

    private void LogBackfillSummary(int totalProcessed, int totalInserted, int totalErrors, TimeSpan elapsed)
    {
        var action = _settings.DryRun ? "Would Be Inserted" : "Inserted";

        Console.WriteLine("\n========================================");
        Console.WriteLine("Backfill Summary");
        Console.WriteLine("========================================");
        Console.WriteLine($"Total Orders Processed: {totalProcessed}");
        Console.WriteLine($"Total Status Feed Entries {action}: {totalInserted}");
        Console.WriteLine($"Total Errors: {totalErrors}");
        Console.WriteLine($"Total elapsed time: {elapsed:hh\\:mm\\:ss}");

        if (_settings.DryRun)
        {
            Console.WriteLine("\nWARNING: DRY RUN MODE - No changes were committed to the database");
        }
    }
}
