using Altinn.Notifications.Triggers.BackgroundServices;
using Altinn.Notifications.Triggers.Configuration;
using Altinn.Notifications.Triggers.Health;

ILogger logger;

var builder = WebApplication.CreateBuilder(args);

ConfigureSetupLogging();
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();

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


void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    // Add services to the container.

    services.AddControllers();
    services.AddHealthChecks().AddCheck<HealthCheck>("notifications_triggers_health_check");

    services.AddSingleton(config);

    services.Configure<PlatformSettings>(config.GetSection("PlatformSettings"));

    builder.Services.AddHostedService<TriggerTimer>();


}