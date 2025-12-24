using Altinn.Notifications.Persistence.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using StatusFeedBackfillTool;
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
    return dataSourceBuilder.Build();
});

// Register repositories
builder.Services.AddSingleton<Altinn.Notifications.Persistence.Repository.OrderRepository>();

// Register the backfill service
builder.Services.AddSingleton<StatusFeedBackfillService>();

var host = builder.Build();

// Run the backfill
using (var scope = host.Services.CreateScope())
{
    var backfillService = scope.ServiceProvider.GetRequiredService<StatusFeedBackfillService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Starting Status Feed Backfill Tool");
        await backfillService.RunBackfill(CancellationToken.None);
        logger.LogInformation("Status Feed Backfill completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during backfill: {Message}", ex.Message);
        return 1;
    }
}

return 0;
