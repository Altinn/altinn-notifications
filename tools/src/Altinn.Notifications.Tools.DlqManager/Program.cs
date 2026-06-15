using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Altinn.Notifications.Tools.DlqManager.Services;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

// ── Settings ────────────────────────────────────────────────────────────────

builder.Services.Configure<PostgreSqlSettings>(
    builder.Configuration.GetSection("PostgreSQLSettings"));

builder.Services.Configure<AsbSettings>(
    builder.Configuration.GetSection("AsbSettings"));

builder.Services.Configure<SmsSendQueueSettings>(
    builder.Configuration.GetSection("SmsSendQueueSettings"));

builder.Services.Configure<PastDueOrdersQueueSettings>(
    builder.Configuration.GetSection("PastDueOrdersQueueSettings"));

// ── Database ─────────────────────────────────────────────────────────────────

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgreSqlSettings>>().Value;

    if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        throw new InvalidOperationException(
            "PostgreSQLSettings:ConnectionString is not configured. " +
            "Set it in appsettings.json or via user secrets.");

    return new NpgsqlDataSourceBuilder(settings.ConnectionString).Build();
});

// ── Repositories ─────────────────────────────────────────────────────────────

builder.Services.AddSingleton<ISmsNotificationRepository, SmsNotificationRepository>();
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// ── Azure Service Bus ────────────────────────────────────────────────────────

builder.Services.AddSingleton<ServiceBusClient>(sp =>
{
    var asbSettings = sp.GetRequiredService<IOptions<AsbSettings>>();

    if (string.IsNullOrWhiteSpace(asbSettings.Value.ConnectionString))
        throw new InvalidOperationException(
            "AsbSettings:ConnectionString is not configured. " +
            "Set it in appsettings.json or via user secrets.");

    return new ServiceBusClient(asbSettings.Value.ConnectionString);
});

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddSingleton<ISmsSendQueueService>(sp =>
{
    var asbSettings = sp.GetRequiredService<IOptions<AsbSettings>>();
    var queueSettings = sp.GetRequiredService<IOptions<SmsSendQueueSettings>>();
    var repository = sp.GetRequiredService<ISmsNotificationRepository>();
    var sbClient = sp.GetRequiredService<ServiceBusClient>();
    return new SmsSendQueueService(asbSettings, queueSettings, repository, sbClient);
});

builder.Services.AddSingleton<IPastDueOrdersQueueService>(sp =>
{
    var asbSettings = sp.GetRequiredService<IOptions<AsbSettings>>();
    var queueSettings = sp.GetRequiredService<IOptions<PastDueOrdersQueueSettings>>();
    var repository = sp.GetRequiredService<IOrderRepository>();
    var sbClient = sp.GetRequiredService<ServiceBusClient>();
    return new PastDueOrdersQueueService(asbSettings, queueSettings, repository, sbClient);
});

// ── Run ───────────────────────────────────────────────────────────────────────

var host = builder.Build();

// Ctrl+C exits immediately — Console.ReadLine() cannot be cancelled, so intercepting
// the signal and setting e.Cancel=true would cause the process to hang at the next prompt.
using var scope = host.Services.CreateScope();

try
{
    var menuService = new ConsoleMenuService(scope.ServiceProvider);
    return await menuService.RunMenuAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}
