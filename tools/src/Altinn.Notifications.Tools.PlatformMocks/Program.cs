using Altinn.Notifications.Tools.PlatformMocks;
using Altinn.Notifications.Tools.PlatformMocks.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Trace;

// appsettings.json is copied to the output folder, so it is found whichever folder the mocks are started from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<PlatformMocksSettings>(builder.Configuration.GetSection(nameof(PlatformMocksSettings)));
builder.Services.AddHostedService<PlatformMocksService>();

// Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT. Without it the requests and responses only go to the console.
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddSource(PlatformMocksService.ActivitySource.Name))
        .UseOtlpExporter();
}

await builder.Build().RunAsync();
