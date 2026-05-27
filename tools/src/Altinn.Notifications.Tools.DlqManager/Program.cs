using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Repositories;
using Altinn.Notifications.Tools.DlqManager.Services;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddSingleton<ISmsSendQueueService, SmsSendQueueService>();

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
