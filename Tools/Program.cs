using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration with user secrets support
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true); // Add this line

// Configure PostgreSQL settings
builder.Services.Configure<PostgreSQLSettings>(
    builder.Configuration.GetSection("PostgreSQLSettings"));

// Register NpgsqlDataSource
var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString!);
    return dataSourceBuilder.Build();
});

// Register the repository
builder.Services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();

var host = builder.Build();

// Use the repository
using (var scope = host.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    try
    {
        var reports = await repository.GetAllAsync(CancellationToken.None);
        Console.WriteLine($"Found {reports.Count} dead delivery reports");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        return;
    }
}
