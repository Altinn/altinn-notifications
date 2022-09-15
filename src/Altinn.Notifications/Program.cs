using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Configuration;

using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core;
using Altinn.Notifications.Health;
using Altinn.Notifications.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration.AzureKeyVault;

using Npgsql.Logging;

using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

ILogger logger;

string vaultApplicationInsightsKey = "ApplicationInsights--InstrumentationKey";

string applicationInsightsKey = string.Empty;

var builder = WebApplication.CreateBuilder(args);

ConfigureSetupLogging();
ConfigureLogging(builder.Logging);
ConfigureServices(builder.Services, builder.Configuration);
SetConfigurationProviders(builder.Configuration);

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
    builder.Configuration.GetValue<string>("PostgreSQLSettings:NotificationsDbAdminPwd"));

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

app.MapHealthChecks("/health");

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
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    services.AddHealthChecks().AddCheck<HealthCheck>("notifications_health_check");

    services.AddSingleton(config);

    services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));
    services.Configure<SmtpSettings>(config.GetSection("SmtpSettings"));

    services.AddSingleton<IEmail, EmailSmtp>();
    services.AddSingleton<INotifications, NotificationsService>();
    services.AddSingleton<INotificationsRepository, NotificationRepository>();

    services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();

}

void SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
    config.SetBasePath(basePath);
    config.AddJsonFile(basePath + "altinn-appsettings/altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);
    if (basePath == "/")
    {
        config.AddJsonFile(basePath + "app/appsettings.json", optional: false, reloadOnChange: true);
    }
    else
    {
        config.AddJsonFile(Directory.GetCurrentDirectory() + "/appsettings.json", optional: false, reloadOnChange: true);
    }

    config.AddEnvironmentVariables();

    ConnectToKeyVaultAndSetApplicationInsights(config);

    config.AddCommandLine(args);
}

void ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
   KeyVaultSettings keyVaultSettings = new KeyVaultSettings();
    config.GetSection("kvSetting").Bind(keyVaultSettings);
    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        logger.LogInformation("Program // Configure key vault client // App");

        string connectionString = $"RunAs=App;AppId={keyVaultSettings.ClientId};" +
                                  $"TenantId={keyVaultSettings.TenantId};" +
                                  $"AppKey={keyVaultSettings.ClientSecret}";
        AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider(connectionString);
        KeyVaultClient keyVaultClient = new KeyVaultClient(
            new KeyVaultClient.AuthenticationCallback(
                azureServiceTokenProvider.KeyVaultTokenCallback));
        config.AddAzureKeyVault(
            keyVaultSettings.SecretUri, keyVaultClient, new DefaultKeyVaultSecretManager());
        try
        {
            SecretBundle secretBundle = keyVaultClient
                .GetSecretAsync(keyVaultSettings.SecretUri, vaultApplicationInsightsKey).Result;

            applicationInsightsKey = secretBundle.Value;
        }
        catch (Exception vaultException)
        {
            logger.LogError($"Unable to read application insights key {vaultException}");
        }
    }
}
