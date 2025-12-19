using Altinn.Notifications.Core.Enums;
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
using Tools.EventGrid;
using Tools.Kafka;

var builder = Host.CreateApplicationBuilder(args);

ConfigureServices(builder);

var host = builder.Build();

await ProcessDeadDeliveryReportsAsync(host);

static void ConfigureServices(HostApplicationBuilder builder)
{
    ConfigureAppSettings(builder);
    ConfigureDatabase(builder);
    ConfigureKafka(builder);
    ConfigureEventGrid(builder);
    RegisterRepositoriesAndServices(builder);
}

static void ConfigureAppSettings(HostApplicationBuilder builder)
{
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>(optional: true);
}

static void ConfigureDatabase(HostApplicationBuilder builder)
{
    builder.Services.Configure<PostgreSqlSettings>(
        builder.Configuration.GetSection("PostgreSQLSettings"));

    var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
    builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString!);
        return dataSourceBuilder.Build();
    });
}

static void ConfigureKafka(HostApplicationBuilder builder)
{
    builder.Services.AddSingleton(sp =>
    {
        var kafkaSettings = new KafkaSettings();
        builder.Configuration.GetSection("KafkaSettings").Bind(kafkaSettings);
        return kafkaSettings;
    });
}

static void ConfigureEventGrid(HostApplicationBuilder builder)
{
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
}

static void RegisterRepositoriesAndServices(HostApplicationBuilder builder)
{
    builder.Services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();
    builder.Services.AddSingleton<ICommonProducer, CommonProducer>();
}

static async Task ProcessDeadDeliveryReportsAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IDeadDeliveryReportRepository>();
    var eventGridClient = scope.ServiceProvider.GetRequiredService<IEventGridClient>();
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

    try
    {
        const int fromId = 20000;
        const int toId = 50000;

        var operationResults = await Util.GetAndMapDeadDeliveryReports(
            repository,
            fromId,
            toId,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        await ProcessAndPostEventsAsync(operationResults, dataSource, eventGridClient);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

static async Task ProcessAndPostEventsAsync(
    IEnumerable<dynamic> operationResults,
    NpgsqlDataSource dataSource,
    IEventGridClient eventGridClient)
{
    foreach (var result in operationResults)
    {
        if (!await ShouldProcessNotificationAsync(dataSource, result))
        {
            continue;
        }

        var eventGridEvent = CreateEventGridEvent(result);
        await Task.Delay(100);

        await PostEventToGridAsync(eventGridClient, eventGridEvent, result);
    }
}

static async Task<bool> ShouldProcessNotificationAsync(NpgsqlDataSource dataSource, dynamic result)
{
    var isInSucceededState = await PostgresUtil.IsEmailNotificationInSucceededState(dataSource, result.OperationId);
    if (!isInSucceededState)
    {
        Console.WriteLine($"Notification {result.NotificationId} with OperationId {result.OperationId} is already marked as Delivered or final. Skipping Event Grid post.");
        return false;
    }

    return true;
}

static EventGridEvent CreateEventGridEvent(dynamic result)
{
    var bd = BinaryData.FromObjectAsJson(new
    {
        messageId = result.OperationId,
        status = Util.MapStatus(result)
    });

    var subject = $"sender/senderid@azure.com/message/{result.OperationId}";
    var eventGridEvent = new EventGridEvent(subject, "Microsoft.Communication.EmailDeliveryReportReceived", "1.0", bd);
    eventGridEvent.EventTime = DateTime.UtcNow;

    return eventGridEvent;
}

static async Task PostEventToGridAsync(IEventGridClient eventGridClient, EventGridEvent eventGridEvent, dynamic result)
{
    var eventArray = new[] { eventGridEvent };

    await eventGridClient.PostEventsAsync(eventArray, CancellationToken.None);

    Console.WriteLine($"✓ Event Grid event posted for notification {result.NotificationId} with OperationId {result.OperationId}");
}
