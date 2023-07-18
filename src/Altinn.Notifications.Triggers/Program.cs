using Altinn.Notifications.Triggers.Health;

var builder = WebApplication.CreateBuilder(args);

IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

string notificationsEndpoint = configuration["PlatformSettings:ApiNotificationsEndpoint"]!;
Console.WriteLine($"Hello, World! \r\n {notificationsEndpoint}");

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<HealthCheck>("notifications_triggers_health_check");

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");


app.Run();
