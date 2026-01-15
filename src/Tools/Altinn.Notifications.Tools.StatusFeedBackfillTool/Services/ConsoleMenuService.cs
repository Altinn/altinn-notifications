using Microsoft.Extensions.DependencyInjection;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces;

namespace Altinn.Notifications.Tools.StatusFeedBackfillTool.Services
{
    public class ConsoleMenuService(IServiceProvider serviceProvider)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task<int> RunMenuAsync()
        {
            Console.WriteLine("Starting Status Feed Backfill Tool\n");

            // Interactive mode selection
            Console.WriteLine("Select operation mode:");
            Console.WriteLine("  1. Discover - Find affected orders and save to file");
            Console.WriteLine("  2. Backfill - Process orders from file and insert status feed entries");
            Console.WriteLine("  3. Generate Test Data - Create test orders for manual testing");
            Console.WriteLine("  4. Cleanup Test Data - Remove all test orders");
            Console.WriteLine("  5. Exit");
            Console.Write("\nEnter choice (1-5): ");

            var choice = Console.ReadLine()?.Trim();

            if (choice == "1")
            {
                var discoveryService = _serviceProvider.GetRequiredService<IOrderDiscoveryService>();
                await discoveryService.Run();
            }
            else if (choice == "2")
            {
                var backfillService = _serviceProvider.GetRequiredService<IStatusFeedBackfillService>();
                await backfillService.Run();
            }
            else if (choice == "3")
            {
                var testDataService = _serviceProvider.GetRequiredService<ITestDataService>();
                await testDataService.GenerateTestData();
            }
            else if (choice == "4")
            {
                var testDataService = _serviceProvider.GetRequiredService<ITestDataService>();

                Console.Write("\nWARNING: This will delete all test orders with sender reference prefix 'backfill-tool-test-'. Continue? (y/n): ");
                var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (confirm == "y" || confirm == "yes")
                {
                    await testDataService.CleanupTestData();
                }
                else
                {
                    Console.WriteLine("Cleanup cancelled.");
                }
            }
            else
            {
                Console.WriteLine("Exiting...");
                return 0;
            }

            Console.WriteLine("\nStatus Feed Backfill Tool completed successfully");
            return 0;
        }
    }
}
