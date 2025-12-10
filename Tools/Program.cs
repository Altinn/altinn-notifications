using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tools;
using Azure.Messaging.EventGrid;


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

// Register Event Grid Client
builder.Services.AddSingleton(sp =>
{
    var baseUrl = builder.Configuration["EventGrid:BaseUrl"] 
        ?? throw new InvalidOperationException("EventGrid:BaseUrl is not configured");
    var accessKey = builder.Configuration["EventGrid:AccessKey"] 
        ?? throw new InvalidOperationException("EventGrid:AccessKey is not configured");
    
    return new EventGridClient(baseUrl, accessKey);
});

// Register the repositories and services
builder.Services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();
builder.Services.AddSingleton<ICommonProducer, CommonProducer>();

var host = builder.Build();

// Use the repository
using (var scope = host.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    var eventGridClient = scope.ServiceProvider.GetRequiredService<EventGridClient>();
    
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

        foreach (var result in operationResults)
        {
            var bd = BinaryData.FromObjectAsJson(new
            {
                messageId = result.OperationId,
                status = result.SendResult.ToString()
            });

            var subject = $"sender/senderid@azure.com/message/{result.OperationId}";

            var eventGridEvent = new EventGridEvent(subject, "Microsoft.Communication.EmailDeliveryReportReceived", "1.0", bd);
            eventGridEvent.EventTime = DateTime.UtcNow;

            var eventArray = new[] { eventGridEvent };

            // Post to Event Grid using reusable client
            var (success, responseBody) = await eventGridClient.PostEventsAsync(
                eventArray, 
                CancellationToken.None);

            if (!success)
            {
                Console.WriteLine($"Failed to post event for notification {result.NotificationId}");
                Console.WriteLine($"Response: {responseBody}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return;
    }
}
