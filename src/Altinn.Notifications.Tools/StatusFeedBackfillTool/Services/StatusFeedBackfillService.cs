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
public class StatusFeedBackfillService(
    OrderRepository orderRepository,
    IOptions<BackfillSettings> settings)
{
    private readonly OrderRepository _orderRepository = orderRepository;
    private readonly BackfillSettings _settings = settings.Value;

    public async Task Run()
    {
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine("=== BACKFILL MODE ===\n");

        var ordersToProcess = await LoadOrdersFromFile();

        if (ordersToProcess.Count == 0)
        {
            stopwatch.Stop();
            Console.WriteLine($"No orders found in file: {_settings.OrderIdsFilePath}");
            Console.WriteLine($"Total elapsed time: {stopwatch.Elapsed:hh\\:mm\\:ss}");
            return;
        }

        Console.WriteLine($"Loaded {ordersToProcess.Count} orders from file");
        Console.WriteLine($"Order IDs File: {_settings.OrderIdsFilePath}\n");

        bool isDryRun = _settings.DryRun;

        Console.Write($"Run in DRY RUN mode? (y/n, default: {(_settings.DryRun ? "y" : "n")}): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(input))
        {
            isDryRun = input == "y" || input == "yes";
        }

        Console.WriteLine($"Dry Run: {isDryRun}\n");

        var (totalProcessed, totalInserted, totalErrors) = await ProcessOrders(ordersToProcess, isDryRun);

        stopwatch.Stop();
        LogBackfillSummary(totalProcessed, totalInserted, totalErrors, isDryRun, stopwatch.Elapsed);
    }

    private async Task<List<Guid>> LoadOrdersFromFile()
    {
        if (!File.Exists(_settings.OrderIdsFilePath))
        {
            Console.WriteLine($"ERROR: File not found: {_settings.OrderIdsFilePath}");
            return [];
        }

        var json = await File.ReadAllTextAsync(_settings.OrderIdsFilePath);
        var orders = JsonSerializer.Deserialize<List<Guid>>(json);
        return orders ?? [];
    }

    private async Task<(int totalProcessed, int totalInserted, int totalErrors)> ProcessOrders(List<Guid> allOrders, bool isDryRun)
    {
        int totalProcessed = 0;
        int totalInserted = 0;
        int totalErrors = 0;
        int currentOrder = 0;
        int totalOrderCount = allOrders.Count;

        foreach (var orderId in allOrders)
        {
            currentOrder++;

            if (currentOrder % 10 == 0 || currentOrder == 1)
            {
                Console.WriteLine($"Processing order {currentOrder}/{totalOrderCount}...");
            }

            try
            {
                if (isDryRun)
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

    private static void LogBackfillSummary(int totalProcessed, int totalInserted, int totalErrors, bool isDryRun, TimeSpan elapsed)
    {
        var action = isDryRun ? "Would Be Inserted" : "Inserted";

        Console.WriteLine("\n========================================");
        Console.WriteLine("Backfill Summary");
        Console.WriteLine("========================================");
        Console.WriteLine($"Total Orders Processed: {totalProcessed}");
        Console.WriteLine($"Total Status Feed Entries {action}: {totalInserted}");
        Console.WriteLine($"Total Errors: {totalErrors}");
        Console.WriteLine($"Total elapsed time: {elapsed:hh\\:mm\\:ss}");

        if (isDryRun)
        {
            Console.WriteLine("\nWARNING: DRY RUN MODE - No changes were committed to the database");
        }
    }
}
