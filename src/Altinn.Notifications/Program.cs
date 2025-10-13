#nullable disable
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Authorization;

using Altinn.Notifications.Authorization;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Health;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Middleware;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Swagger;
using Altinn.Notifications.Telemetry;

using AltinnCore.Authentication.JwtCookie;

using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Npgsql;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;

ILogger logger;

var builder = WebApplication.CreateBuilder(args);

ConfigureWebHostCreationLogging();

SetConfigurationProviders(builder.Configuration);

ConfigureApplicationLogging(builder.Logging);

ConfigureServices(builder.Services, builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    string bearerSecuritySchemaName = "bearerAuth";
    bool includeUnauthorizedAndForbiddenResponses = true;

    options.OperationFilter<SecurityRequirementsOperationFilter>(includeUnauthorizedAndForbiddenResponses, bearerSecuritySchemaName);

    options.AddSecurityDefinition(bearerSecuritySchemaName, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    IncludeXmlComments(options);
    
    options.EnableAnnotations();
    options.UseInlineDefinitionsForEnums();
    options.SchemaFilter<SwaggerDefaultValues>();
    options.OperationFilter<AddResponseHeadersFilter>();
    
    options.ExampleFilters();
    
    options.AddServer(new OpenApiServer()
    {
        Url = "https://platform.tt02.altinn.no/",
        Description = "TT02"
    });
    options.AddServer(new OpenApiServer()
    {
        Url = "https://platform.altinn.no/",
        Description = "Production"
    });
    options.AddServer(new OpenApiServer()
    {
        Url = builder.Configuration.GetValue<string>("GeneralSettings:BaseUri")
              ?? "https://localhost:5090/",
        Description = "Local development"
    });
});

builder.Services.AddSwaggerExamplesFromAssemblies(Assembly.GetEntryAssembly());

var app = builder.Build();

app.SetUpPostgreSql(builder.Environment.IsDevelopment(), builder.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.UseOrgExtractor();

await app.RunAsync();

void ConfigureWebHostCreationLogging()
{
    var logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Altinn.Platform.Notifications.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

void ConfigureApplicationLogging(ILoggingBuilder logging)
{
    logging.AddOpenTelemetry(builder =>
    {
        builder.IncludeFormattedMessage = true;
        builder.IncludeScopes = true;
    });
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    logger.LogInformation("Program // ConfigureServices");

    var attributes = new List<KeyValuePair<string, object>>(2)
    {
        KeyValuePair.Create("service.name", (object)"platform-notifications"),
    };

    services.AddHttpContextAccessor();

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
            if (builder.Environment.IsDevelopment())
            {
                tracing.SetSampler(new AlwaysOnSampler());
            }

            tracing.AddAspNetCoreInstrumentation();

            tracing.AddHttpClientInstrumentation();

            tracing.AddProcessor<RequestFilterProcessor>();

            tracing.AddNpgsql();
        });

    AddAzureMonitorTelemetryExporters(services, config);

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Insert(0, new JsonStringEnumConverter());
    });
    services.AddControllersWithViews();

    services.AddHealthChecks().AddCheck<HealthCheck>("notifications_health_check");

    services.AddSingleton(config);

    GeneralSettings generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();
    services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
    services.AddAuthentication(JwtCookieDefaults.AuthenticationScheme)
        .AddJwtCookie(JwtCookieDefaults.AuthenticationScheme, options =>
        {
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

    AddAuthorizationRulesAndHandlers(services, config);

    ResourceLinkExtensions.Initialize(generalSettings.BaseUri);
    AddInputModelValidators(services);
    services.AddCoreServices(config);
    services.AddAuthorizationService(config);
    services.AddKafkaServices(config);
    services.AddAltinnClients(config);
    services.AddPostgresRepositories(config);
}

void SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
    config.SetBasePath(basePath);
    config.AddJsonFile(basePath + "altinn-appsettings/altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);

    AddSecretsFromKeyVault(config);
}

void AddSecretsFromKeyVault(ConfigurationManager config)
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

        config.AddAzureKeyVault(new Uri(keyVaultSettings.SecretUri), new DefaultAzureCredential());
    }
}

void AddAzureMonitorTelemetryExporters(IServiceCollection services, IConfiguration config)
{
    var instrumentationKey = config.GetValue<string>("ApplicationInsights:InstrumentationKey");

    if (string.IsNullOrEmpty(instrumentationKey))
    {
        return;
    }

    var applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", instrumentationKey);

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

void AddAuthorizationRulesAndHandlers(IServiceCollection services, IConfiguration config)
{
    services.AddAuthorizationBuilder()
        .AddPolicy(AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS, policy =>
        {
            policy.Requirements.Add(new CreateScopeOrAccessTokenRequirement(AuthorizationConstants.SCOPE_NOTIFICATIONS_CREATE));
        });

    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();

    // services required for access token handler
    services.AddMemoryCache();
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProvider>();
    services.Configure<Altinn.Common.AccessToken.Configuration.KeyVaultSettings>(config.GetSection("kvSetting"));
    services.AddSingleton<IAuthorizationHandler, AccessTokenHandler>();
}

void AddInputModelValidators(IServiceCollection services)
{
    ValidatorOptions.Global.LanguageManager.Enabled = false;
    services.AddValidatorsFromAssemblyWithDuplicateCheck(typeof(Program).Assembly);
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
