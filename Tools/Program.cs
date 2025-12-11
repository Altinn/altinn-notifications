using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

// Register Event Grid Client
builder.Services.Configure<EventGridSettings>(builder.Configuration.GetSection("EventGrid"));
builder.Services.AddHttpClient<IEventGridClient, EventGridClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IOptions<EventGridSettings>>().Value;
    if (string.IsNullOrWhiteSpace(cfg.BaseUrl))
    {
        throw new InvalidOperationException("EventGrid:BaseUrl is not configured");
    }

    client.BaseAddress = new Uri(cfg.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
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
        var fromId = 20000;
        var toId = 50000;
        
        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            fromId,
            toId,
            Altinn.Notifications.Core.Enums.DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);
            
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        foreach (var result in operationResults)
        {

            var isSucceeded = await Util.IsEmailNotificationSucceeded(dataSource, result.OperationId);
            if (!isSucceeded)
            {
                Console.WriteLine($"Notification {result.NotificationId} with OperationId {result.OperationId} is already marked as Delivered or final. Skipping Event Grid post.");
                continue;
            }

            var bd = BinaryData.FromObjectAsJson(new
            {
                messageId = result.OperationId,
                status = MapStatus(result)
            });

            var subject = $"sender/senderid@azure.com/message/{result.OperationId}";

            var eventGridEvent = new EventGridEvent(subject, "Microsoft.Communication.EmailDeliveryReportReceived", "1.0", bd);
            eventGridEvent.EventTime = DateTime.UtcNow;

            var eventArray = new[] { eventGridEvent };

            Task.Delay(100).Wait();

            // Post to Event Grid using reusable client
            var (_, _) = await eventGridClient.PostEventsAsync(
                eventArray,
                CancellationToken.None);

            Console.WriteLine("✓ Event Grid event posted for notification " +
                              $"{result.NotificationId} with OperationId {result.OperationId}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return;
    }
}

static string? MapStatus(Altinn.Notifications.Core.Models.Notification.EmailSendOperationResult result)
{
    if (string.Equals(result.SendResult.ToString(), "Failed_Bounced", StringComparison.OrdinalIgnoreCase))
    {
        return "Bounced";
    }
    if (string.Equals(result.SendResult.ToString(), "Failed_SupressedRecipient", StringComparison.OrdinalIgnoreCase))
    {
        return "Suppressed";
    }

    return result.SendResult.ToString();
}
