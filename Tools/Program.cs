using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Repository;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tools;

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
        var fromDate = new DateTime(2025, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        var reason = "test"; // Replace with actual reason
        var channel = Altinn.Notifications.Core.Enums.DeliveryReportChannel.AzureCommunicationServices; 
        var reports = await repository.GetAllAsync(fromDate, reason, channel, CancellationToken.None);
        Console.WriteLine($"Found {reports.Count} dead delivery reports");


        var producer = scope.ServiceProvider.GetRequiredService<ICommonProducer>();

        var sendOperationResult = new EmailSendOperationResult
        {
            OperationId = "abcdefgh1234",
            SendResult = Altinn.Notifications.Core.Enums.EmailNotificationResultType.Delivered
        };
        var topic = builder.Configuration["KafkaSettings:EmailStatusUpdatedTopicName"];

        if (string.IsNullOrEmpty(topic))
        {
            Console.WriteLine("EmailStatusUpdatedTopicName is not configured.");
            return;
        }

        var result = await producer.ProduceAsync(topic, sendOperationResult.Serialize());

        Console.WriteLine($"Message produced to topic {topic}: {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        return;
    }
}
