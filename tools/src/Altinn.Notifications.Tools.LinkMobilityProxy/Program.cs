using Altinn.Notifications.Tools.LinkMobilityProxy;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Trace;

// appsettings.json is copied to the output folder, so it is found whichever folder the proxy is started from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<LinkMobilityProxySettings>(builder.Configuration.GetSection(nameof(LinkMobilityProxySettings)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDeliveryReportSender, HttpDeliveryReportSender>();
builder.Services.AddSingleton<DeliveryReportService>();
builder.Services.AddHostedService(services => services.GetRequiredService<DeliveryReportService>());
builder.Services.AddHostedService<LinkMobilityProxyService>();

// Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT. Without it the masked payloads only go to the console.
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddSource(LinkMobilityProxyService.ActivitySource.Name))
        .UseOtlpExporter();
}

await builder.Build().RunAsync();
