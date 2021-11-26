using System.Text.Json.Serialization;

using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core;
using Altinn.Notifications.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence;

using Npgsql.Logging;

using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

ILogger logger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

ConfigureSetupLogging();
ConfigureLogging(builder.Logging);
ConfigureServices(builder.Services, builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Trace, true, true);

ConsoleTraceService traceService = new ConsoleTraceService { IsDebugEnabled = true };

if (builder.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
{

    string connectionString = string.Format(
    builder.Configuration.GetValue<string>("PostgreSQLSettings:AdminConnectionString"),
    builder.Configuration.GetValue<string>("PostgreSQLSettings:EventsDbAdminPwd"));

    app.UseYuniql(
        new PostgreSqlDataService(traceService),
        new PostgreSqlBulkImportService(traceService),
        traceService,
        new Yuniql.AspNetCore.Configuration
        {
            Workspace = Path.Combine(Environment.CurrentDirectory, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath")),
            ConnectionString = connectionString,
            IsAutoCreateDatabase = false,
            IsDebug = true
        });
}

app.UseAuthorization();

app.MapControllers();

app.Run();


void ConfigureSetupLogging()
{
    var logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Altinn.Platform.Register.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

void ConfigureLogging(ILoggingBuilder logging)
{
    // The default ASP.NET Core project templates call CreateDefaultBuilder, which adds the following logging providers:
    // Console, Debug, EventSource
    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1

    // Clear log providers
    logging.ClearProviders();

    // If not application insight is available log to console
    logging.AddFilter("Microsoft", LogLevel.Warning);
    logging.AddFilter("System", LogLevel.Warning);
    logging.AddConsole();
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    logger.LogInformation("Program // ConfigureServices");

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    services.AddSingleton(config);

    services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));
    services.Configure<SmtpSettings>(config.GetSection("SmtpSettings"));

    services.AddSingleton<IEmail, EmailSmtp>();
    services.AddSingleton<INotifications, NotificationsService>();
    services.AddSingleton<INotificationsRepository, NotificationRepository>();
}