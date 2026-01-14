using Altinn.Notifications.Persistence.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Configuration;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Services;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces;

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


// Register services with interfaces for testability
builder.Services.AddSingleton<IOrderDiscoveryService, OrderDiscoveryService>();
builder.Services.AddSingleton<IStatusFeedBackfillService, StatusFeedBackfillService>();
builder.Services.AddSingleton<ITestDataService, TestDataService>();

var host = builder.Build();

// Run the tool
using (var scope = host.Services.CreateScope())
{
    try
    {
        var menuService = new ConsoleMenuService(scope.ServiceProvider);
        int result = await menuService.RunMenuAsync();
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: An error occurred during backfill: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return 1;
    }
}

