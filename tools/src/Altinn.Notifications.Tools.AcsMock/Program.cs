using Altinn.Notifications.Tools.AcsMock;
using Altinn.Notifications.Tools.AcsMock.Configuration;

using OpenTelemetry;
using OpenTelemetry.Trace;

// appsettings.json is in the output folder, so it is found whichever folder the mock is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddUserSecrets<AcsMockSettings>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<AcsMockSettings>(builder.Configuration.GetSection(nameof(AcsMockSettings)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EmailOperationStore>();
builder.Services.AddSingleton<IDeliveryReportSender, ServiceBusDeliveryReportSender>();
builder.Services.AddSingleton<EmailMirror>();
builder.Services.AddHostedService(services => services.GetRequiredService<EmailMirror>());
builder.Services.AddSingleton<DeliveryReportService>();
builder.Services.AddHostedService(services => services.GetRequiredService<DeliveryReportService>());
builder.Services.AddHttpClient(AcsEndpoints.FallbackClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);

// Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT. Without it the masked payloads only go to the console.
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
        .UseOtlpExporter();
}

var port = builder.Configuration.GetValue("AcsMockSettings:Port", 5095);
builder.WebHost.UseUrls($"http://localhost:{port}");

var app = builder.Build();
app.MapAcsEndpoints();
app.Run();
