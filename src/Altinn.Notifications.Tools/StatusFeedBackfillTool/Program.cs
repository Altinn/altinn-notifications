using Altinn.Notifications.Persistence.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StatusFeedBackfillTool.Configuration;
using StatusFeedBackfillTool.Services;
using System.Diagnostics.CodeAnalysis;

[assembly: ExcludeFromCodeCoverage]

var builder = Host.CreateApplicationBuilder(args);

// Add configuration with user secrets support
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true);

// Configure PostgreSQL settings
builder.Services.Configure<PostgreSqlSettings>(
    builder.Configuration.GetSection("PostgreSQLSettings"));

// Configure discovery settings
builder.Services.Configure<DiscoverySettings>(
    builder.Configuration.GetSection("DiscoverySettings"));

// Configure backfill settings
builder.Services.Configure<BackfillSettings>(
    builder.Configuration.GetSection("BackfillSettings"));

// Register NpgsqlDataSource
var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("PostgreSQLSettings:ConnectionString is not configured");
}

builder.Services.AddSingleton(sp =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.EnableDynamicJson();
    return dataSourceBuilder.Build();
});

// Register repositories
builder.Services.AddSingleton<Altinn.Notifications.Persistence.Repository.OrderRepository>();
builder.Services.AddSingleton<Altinn.Notifications.Core.Persistence.IOrderRepository>(sp => 
    sp.GetRequiredService<Altinn.Notifications.Persistence.Repository.OrderRepository>());

// Register repositories for test data service
builder.Services.AddSingleton<Altinn.Notifications.Persistence.Repository.EmailNotificationRepository>();
builder.Services.AddSingleton<Altinn.Notifications.Core.Persistence.IEmailNotificationRepository>(sp => 
    sp.GetRequiredService<Altinn.Notifications.Persistence.Repository.EmailNotificationRepository>());

// Register services
builder.Services.AddSingleton<OrderDiscoveryService>();
builder.Services.AddSingleton<StatusFeedBackfillService>();
builder.Services.AddSingleton<TestDataService>();

var host = builder.Build();

// Run the tool
using (var scope = host.Services.CreateScope())
{
    try
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
            var discoveryService = scope.ServiceProvider.GetRequiredService<OrderDiscoveryService>();
            await discoveryService.Run(CancellationToken.None);
        }
        else if (choice == "2")
        {
            var backfillService = scope.ServiceProvider.GetRequiredService<StatusFeedBackfillService>();
            await backfillService.Run(CancellationToken.None);
        }
        else if (choice == "3")
        {
            var testDataService = scope.ServiceProvider.GetRequiredService<TestDataService>();
            await testDataService.GenerateTestData();
        }
        else if (choice == "4")
        {
            var testDataService = scope.ServiceProvider.GetRequiredService<TestDataService>();
            
            Console.Write("\nWARNING: This will delete all test orders with sender reference prefix 'backfill-tool-test-'. Continue? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            
            if (confirm == "y" || confirm == "yes")
            {
                await testDataService.CleanupTestData(CancellationToken.None);
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: An error occurred during backfill: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return 1;
    }
}

return 0;
