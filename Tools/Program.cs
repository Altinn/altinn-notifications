using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tools;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration with user secrets support
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true);

// Configure PostgreSQL settings
builder.Services.Configure<PostgreSqlSettings>(
    builder.Configuration.GetSection("PostgreSQLSettings"));

// Register NpgsqlDataSource
var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString!);
    return dataSourceBuilder.Build();
});

// Register KafkaSettings directly (not using Configure)
builder.Services.AddSingleton(sp =>
{
    var kafkaSettings = new KafkaSettings();
    builder.Configuration.GetSection("KafkaSettings").Bind(kafkaSettings);
    return kafkaSettings;
});

// Register the repository
builder.Services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();
builder.Services.AddSingleton<ICommonProducer, CommonProducer>();

var host = builder.Build();

// Use the repository
using (var scope = host.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    try
    {
        var fromId = 43716;
        var toId = 43716;
        
        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            fromId,
            toId,
            Altinn.Notifications.Core.Enums.DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        var producer = scope.ServiceProvider.GetRequiredService<ICommonProducer>();

        await Util.ProduceMessagesToKafka(producer, builder.Configuration["KafkaSettings:EmailStatusUpdatedTopicName"], operationResults);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        return;
    }
}
