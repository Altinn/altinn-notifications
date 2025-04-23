using System.Reflection;

using Altinn.Notifications.Email.Configuration;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Health;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Telemetry;

using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Security.KeyVault.Secrets;

using Microsoft.Extensions.FileProviders;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;

ILogger logger;
const string AppInsightsKeyName = "ApplicationInsights--InstrumentationKey";
string applicationInsightsConnectionString = string.Empty;

WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);

ConfigureWebHostCreationLogging();

await SetConfigurationProviders(appBuilder.Configuration);

ConfigureApplicationLogging(appBuilder.Logging);

ConfigureServices(appBuilder.Services, appBuilder.Configuration);

appBuilder.Services.AddEndpointsApiExplorer();
appBuilder.Services.AddSwaggerGen(c =>
{
    IncludeXmlComments(c);
    c.EnableAnnotations();
    c.OperationFilter<AddResponseHeadersFilter>();
});

var app = appBuilder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

Configure();

await app.RunAsync();

void ConfigureWebHostCreationLogging()
{
    var logFactory = LoggerFactory.Create(logBuilder =>
    {
        logBuilder
            .AddFilter("Altinn.Notifications.Email.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    if (Directory.Exists("/altinn-appsettings"))
    {
        IFileProvider fileProvider = new PhysicalFileProvider("/altinn-appsettings");
        config.AddJsonFile(fileProvider, "altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);
    }
    else
    {
        logger.LogWarning("Expected directory \"/altinn-appsettings\" not found.");
    }

    await ConnectToKeyVaultAndSetApplicationInsights(config);
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

        KeyVaultSecret keyVaultSecret = await client.GetSecretAsync(AppInsightsKeyName);
        applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", keyVaultSecret.Value);
    }
}

void ConfigureApplicationLogging(ILoggingBuilder logging)
{
    logging.AddOpenTelemetry(builder =>
    {
       builder.IncludeFormattedMessage = true;
       builder.IncludeScopes = true; 
    });
}

void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
{
    var attributes = new List<KeyValuePair<string, object>>(2)
    {
        KeyValuePair.Create("service.name", (object)"platform-notifications-email"),
    };

    services.AddOpenTelemetry()
        .ConfigureResource(resourceBuilder => resourceBuilder.AddAttributes(attributes))
        .WithMetrics(metrics => 
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "System.Net.Http");
        })
        .WithTracing(tracing => 
        {
            if (appBuilder.Environment.IsDevelopment())
            {
                tracing.SetSampler(new AlwaysOnSampler());
            }

            tracing.AddAspNetCoreInstrumentation();
            tracing.AddProcessor(new RequestFilterProcessor(new HttpContextAccessor()));
            tracing.AddHttpClientInstrumentation();
        });

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        AddAzureMonitorTelemetryExporters(services, applicationInsightsConnectionString);
    }

    services.AddControllers();
    services.AddHealthChecks().AddCheck<HealthCheck>("notifications_emails_health_check");

    services.AddCoreServices(configuration);
    services.AddIntegrationServices(configuration);
}

static void AddAzureMonitorTelemetryExporters(IServiceCollection services, string applicationInsightsConnectionString)
{
    services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddAzureMonitorLogExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
    services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddAzureMonitorMetricExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
    services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
}

void Configure()
{
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
}

void IncludeXmlComments(SwaggerGenOptions swaggerGenOptions)
{
    try
    {
        string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        swaggerGenOptions.IncludeXmlComments(xmlPath);
    }
    catch (Exception e)
    {
        logger.LogWarning(e, "Program // Exception when attempting to include the XML comments file(s).");
    }
}
