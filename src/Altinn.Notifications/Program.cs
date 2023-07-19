#nullable disable
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Health;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Models;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Validators;

using AltinnCore.Authentication.JwtCookie;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using FluentValidation;

using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.IdentityModel.Tokens;

using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

ILogger logger;

string vaultApplicationInsightsKey = "ApplicationInsights--InstrumentationKey";

string applicationInsightsConnectionString = string.Empty;

var builder = WebApplication.CreateBuilder(args);

ConfigureSetupLogging();
ConfigureLogging(builder.Logging);
ConfigureServices(builder.Services, builder.Configuration);
await SetConfigurationProviders(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
ConfigurePostgreSql(builder.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
            .AddFilter("Altinn.Platform.Notifications.Program", LogLevel.Debug)
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

    // Setup up application insight if applicationInsightsKey   is available
    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        // Add application insights https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger
        logging.AddApplicationInsights(
            configureTelemetryConfiguration: (config) => config.ConnectionString = applicationInsightsConnectionString,
            configureApplicationInsightsLoggerOptions: (options) => { });

        // Optional: Apply filters to control what logs are sent to Application Insights.
        // The following configures LogLevel Information or above to be sent to
        // Application Insights for all categories.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Warning);

        // Adding the filter below to ensure logs of all severity from Program.cs
        // is sent to ApplicationInsights.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Trace);
    }
    else
    {
        // If not application insight is available log to console
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddConsole();
    }
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    logger.LogInformation("Program // ConfigureServices");

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Insert(0, new JsonStringEnumConverter());
    });

    services.AddHealthChecks().AddCheck<HealthCheck>("notifications_health_check");

    services.AddSingleton(config);

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel() { StorageFolder = "/tmp/logtelemetry" });

        services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
        {
            ConnectionString = applicationInsightsConnectionString
        });

        services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
    }

    services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
    services.AddAuthentication(JwtCookieDefaults.AuthenticationScheme)
          .AddJwtCookie(JwtCookieDefaults.AuthenticationScheme, options =>
          {
              GeneralSettings generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();
              options.JwtCookieName = generalSettings.JwtCookieName;
              options.MetadataAddress = generalSettings.OpenIdWellKnownEndpoint;
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  ValidateIssuerSigningKey = true,
                  ValidateIssuer = false,
                  ValidateAudience = false,
                  RequireExpirationTime = true,
                  ValidateLifetime = true,
                  ClockSkew = TimeSpan.Zero
              };

              if (builder.Environment.IsDevelopment())
              {
                  options.RequireHttpsMetadata = false;
              }
          });

    AddInputModelValidators(services);
    services.AddCoreServices(config);
    services.AddKafkaServices(config);
    PostgreSqlSettings postgresSettings = config.GetSection("PostgreSqlSettings").Get<PostgreSqlSettings>();
    services.AddPostgresRepositories(postgresSettings);
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
    config.SetBasePath(basePath);
    config.AddJsonFile(basePath + "altinn-appsettings/altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);
    config.AddEnvironmentVariables();

    await ConnectToKeyVaultAndSetApplicationInsights(config);

    config.AddCommandLine(args);
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);
    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        logger.LogInformation("Program // Configure key vault client // App");
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", keyVaultSettings.ClientId);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", keyVaultSettings.ClientSecret);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", keyVaultSettings.TenantId);
        var azureCredentials = new DefaultAzureCredential();

        config.AddAzureKeyVault(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        SecretClient client = new(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        try
        {
            KeyVaultSecret keyVaultSecret = await client.GetSecretAsync(vaultApplicationInsightsKey);
            applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", keyVaultSecret.Value);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, $"Unable to read application insights key.");
        }
    }
}

void AddInputModelValidators(IServiceCollection services)
{
    services.AddSingleton<IValidator<EmailNotificationOrderRequestExt>, EmailNotificationOrderRequestValidator>();
}

void ConfigurePostgreSql(ConfigurationManager config)
{
    PostgreSqlSettings postgreSettings = new();
    config.GetSection("PostgreSQLSettings").Bind(postgreSettings);
    if (postgreSettings.EnableDBConnection)
    {
        ConsoleTraceService traceService = new() { IsDebugEnabled = true };

        string connectionString = string.Format(postgreSettings.AdminConnectionString, postgreSettings.NotificationsDbAdminPwd);

        string fullWorkspacePath = builder.Environment.IsDevelopment() ?
            Path.Combine(Directory.GetParent(Environment.CurrentDirectory).FullName, postgreSettings.MigrationScriptPath) :
            Path.Combine(Environment.CurrentDirectory, postgreSettings.MigrationScriptPath);

        app.UseYuniql(
            new PostgreSqlDataService(traceService),
            new PostgreSqlBulkImportService(traceService),
            traceService,
            new Configuration
            {
                Workspace = fullWorkspacePath,
                ConnectionString = connectionString,
                IsAutoCreateDatabase = false,
                IsDebug = postgreSettings.EnableDebug
            });
    }
}